# Research — 문서 전체 검토 (task 문서 제외)

**작성일:** 2026-08-25
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-25/02_31_document-review-consistency/`
**성격:** 조사 전용 — 이 단계에서 실제 문서는 **한 글자도 고치지 않는다.** 무엇을 어떻게 고칠지는 `Plan.md`, 실제 수정은 사용자 승인 후 별도 단계다.

---

## 0. 이 작업이 무엇인가 (자연어 설명)

이 프로젝트는 사람과 AI 에이전트가 함께 일합니다. 그래서 "무엇을 어떻게 만들기로 했는지"가 코드가 아니라 **문서**에 적혀 있고, 새 작업을 시작하는 사람(또는 에이전트)은 코드를 읽기 전에 문서를 먼저 읽습니다.

문제는 문서가 74개나 되고, **같은 사실이 여러 문서에 나뉘어 적혀 있다**는 점입니다. 어느 한 곳만 고치고 나머지를 그대로 두면, 나머지는 조용히 거짓말이 됩니다. 겉보기에는 멀쩡한 문서라서 아무도 눈치채지 못합니다.

이번 작업은 그렇게 **문서끼리 서로 다른 말을 하고 있는 자리를 전부 찾아내는 일**입니다. 크게 두 가지를 봅니다.

1. **서로 어긋난 곳** — 예를 들어 어떤 문서는 "스킬 건물은 이미 다 만들었고 실기 테스트도 통과했다"고 적혀 있는데, 그 문서를 안내하는 목차 문서 4곳은 전부 "아직 안 만들었다"고 적혀 있습니다. 이 상태에서 새 작업을 시작하면, **이미 완성된 기능을 처음부터 다시 만들려고 달려들 수 있습니다.**
2. **자동 검사기가 못 보는 구간** — 이 프로젝트에는 `Tools/check_docs.py` 라는 문서 검사 도구가 있고, 이번에 돌려 보니 "문제 없음"이 나왔습니다. 그런데 그건 도구가 보는 범위 안에서 문제가 없다는 뜻이지, 문서가 멀쩡하다는 뜻이 아닙니다. **도구가 애초에 들여다보지 않는 사각지대가 어디까지인지**를 이번에 숫자로 확인했습니다.

즉 이번 조사의 결론은 "검사기가 0건이라고 해서 안심할 수 없다"는 것이고, 그 근거를 아래에 항목별로 정리했습니다.

---

## 1. 검토 범위

### 포함
| 구분 | 대상 |
|---|---|
| 리포지토리 루트 | `CLAUDE.md` · `AGENTS.md` · `CONTEXT.md` |
| 설계·규칙 문서 | `Assets/_Project/Docs/` 의 상시 참조 문서 전체 — GDD / TDD / `GameSystemRules.md` 인덱스 + 하위 12종 / `Assets/` 6종 / `StatsReference.md` / `LogRules.md` / `AuthSystemRules.md` / `UIGuidelines.md` / `WORKFLOW.md` / `Skills/SKILLS_GUIDE.md` |
| 프로젝트 관리 | `PROJECT_STATUS.md` · `ROADMAP.md` · `WORK_HISTORY.md` · `AABSizeOptimization.md` · `UnusedAssetAudit.md` |
| 에이전트 설정·메모리 | `.claude/MEMORY.md` · `.claude/agents/*.md` 6개 · `.claude/agent-memory/**` 31개 · `.claude/mistakes.md` · `.claude/agent-memory/_baseline.json` |
| 도구 | `Tools/check_docs.py` — **검사기가 선언한 역할을 실제로 수행하는지**의 관점에서만 |

### 제외
- `Assets/_Project/Docs/_Tasks/` · `Assets/_Project/Docs/_Logs/` — 이력 기록. `WORKFLOW.md` [11]③ 이 **검사 대상에서 제외되며 소급 수정하지 않는다**고 규정한다.
- 외부 스킬 (`.agents/skills/` · `.claude/skills/`) — 이 프로젝트가 작성·관리하는 문서가 아니다.

### 규모
**md 74개 + 검사기 1개.**

---

## 2. 조사 방법 (무엇을 실제로 실행했는가)

| 방법 | 내용 |
|---|---|
| ① 검사기 실행 | `python3 Tools/check_docs.py` 를 리포지토리 루트에서 실행하고, 출력 7종과 종료 코드를 확인 |
| ② 백틱 경로 전수 대조 | 검토 범위 안의 `` `경로.확장자` `` 표기를 전부 뽑아 리포지토리 실제 파일과 **접미사 매칭**으로 대조. 접미사 매칭을 쓴 이유는 상대 경로 표기(`AABSizeOptimization.md` 처럼 폴더를 생략한 것)를 오탐으로 세지 않기 위해서다 |
| ③ 기준값 대 실측 | `.claude/agent-memory/_baseline.json` 의 `folders` 값을 실제 폴더 행수와 6개 폴더 전부 대조 |
| ④ 목차–본문 diff | `.claude/mistakes.md` 의 목차 줄과 본문 H2 제목을 뽑아 diff |
| ⑤ 문자열 전수 검색 | 죽은 윈도우 절대경로 · `Write` 도구 지시 문구 · 「200행」 기준 서술 등을 확장자 구분 없이 리포지토리 전체에서 검색 |
| ⑥ 코드 열람 | `Tools/check_docs.py` 의 정규식·`--root` 기본값·`collect_files()` 대상 범위를 직접 읽어 **검사기가 무엇을 보지 않는지** 확인 |

> **표기 규약(중요):** 아래 본문에서 행 번호를 적을 때는 **반드시 그 줄의 특징적인 문구를 함께** 적었다. `.claude/mistakes.md` 「2026-08-24 행 번호로 위치를 가리켜 전부 어긋남」 사고가 있었기 때문이다 — 문서 위쪽에 한 줄만 추가돼도 아래 행 번호는 전부 밀린다. **행 번호는 참고값이고, 문구가 진짜 주소다.**

---

## 3. 발견 항목 요약

| ID | 심각도 | 한 줄 요약 |
|---|---|---|
| [A] | 🔴 | 스킬 건물 시스템의 구현 상태가 SSoT(구현 완료)와 인덱스 4곳(미구현)에서 정반대 |
| [B] | 🔴 | 에이전트 정의 6개 중 4개가 자기 파일 안에서 「`Write` 로 메모리 갱신하라」와 「`Write` 금지」를 동시에 지시 |
| [C] | 🔴 | 이미 거짓으로 판명된 「메모리 200행」 기준이 **3곳**에 사본으로 남아 있음 (인계 2곳 + 이번에 1곳 추가 발견) |
| [D] | 🟡 | 「구현 완료 후 QA 필수」와 「사용자 지시 없이 QA 금지」가 3개 문서에서 충돌 |
| [E] | 🟡 | 존재하지 않는 `BuildAssetOptimizationReport.md` 를 **7곳**이 참조 (인계 2곳 + 이번에 5곳 추가 발견) |
| [F] | 🟡 | 백틱 경로 전수 대조 결과 실제로 어긋난 참조 발견 (경로 오류 3건 · 사실 주장 불일치 1건 · 성격 판단 필요 다수) |
| [G] | 🟡 | 검사기의 확인된 사각지대 3가지 — 백틱 경로 미검사 · `.claude/` 미검사 · 사실 충돌 원리적 불가 |
| [H] | 🟡 | `PROJECT_STATUS` / `ROADMAP` / `WORK_HISTORY` 의 역할 경계가 무너져 같은 서사가 3~4중 중복 |
| [I] | 🟢 | `.claude/MEMORY.md` 에 작업 이력 3개 절이 섞여 있음 (갱신 규칙 4 위반) |
| [J] | 🟢 | `.claude/agents/document-manager.md` 가 `AGENTS.md` 표를 통째로 복제했고 이미 한 행이 어긋남 |
| [K] | 🟢 | `CLAUDE.md` 마지막 줄 `[7]~[12]` 가 어느 체계의 번호인지 문서에 없음 |
| [L] | 🟢 | `CONTEXT.md` 가 인덱스에만 있고 갱신 책임자가 없음 |
| [M] | ⚪ 확인 필요 | `_baseline.json` 의 `change_log` 서술과 현재 `folders` 값이 어긋나 보임 — **단정하지 않음** |
| [N] | ⚪ 이상 없음 | 검사했으나 문제가 없었던 항목 5가지 |

> **「인계 대비 추가 발견」 표시:** 이번 조사에서 인계받은 목록보다 넓게 확인된 항목은 본문에 **`🆕 이번 조사에서 추가 확인`** 으로 표시했다. 인계 수치를 그대로 옮겨 적지 않고 재실측하는 것은 `document-manager` 메모리의 확립된 절차다.

---

## 4. 검사기 실행 결과 (사실)

```
python3 Tools/check_docs.py   →  7종 전부 "이상 없음", 종료 코드 0
```

`.claude/agent-memory/_baseline.json` 의 `folders` 값과 실제 폴더 행수를 6개 폴더 전부 대조한 결과 **완전 일치**한다.

| 폴더 | 기준값 행수 | 파일 수 |
|---|---|---|
| asset-prompt-crafter | 166 | 1 |
| document-manager | 228 | 1 |
| game-design-lead | 285 | 3 |
| game-programmer | 2561 | 19 |
| project-orchestrator | 592 | 3 |
| qa-tester | 812 | 3 |

→ **검사 `[6]`(고아 토픽) · `[7]`(폴더 행수 감소)은 정상 작동 중이다.**

⚠️ **그러나 이 "0건"은 문서가 멀쩡하다는 뜻이 아니다.** 아래 [A]~[F] 는 전부 이 0건 상태에서 발견된 것이고, 그 이유는 [G] 에 정리했다.

---

## 5. 🔴 [A] 스킬 건물 시스템 — 구현 상태가 SSoT와 인덱스 4곳에서 정반대

### 무엇이 SSoT라고 선언돼 있는가

`Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md` 는 문서 최상단(3행 부근, 인용 블록 `**이 문서는 스킬 건물 시스템의 단일 소스(SSoT)입니다.**`)에서 이렇게 선언한다.

> 다른 문서(GameDesignDocument, ROADMAP 등)에서 스킬 건물을 다룰 때는 상세 내용을 이 문서로 연결하고, 개별 문서에는 요약과 참조만 둡니다.

같은 문서의 구현 상태 서술 두 곳:

| 위치 | 원문 (특징 문구) |
|---|---|
| 16행 부근 | `**구현 상태: Phase 1(타입 A·B + 조준/UI/좌표화/렌더링) + Phase 2(타입 C 전역 상태변경) 모두 구현 완료 · 실기기 테스트 PASS (Phase 1: 2026-08-04, Phase 2: 2026-08-05 실기+멀티(클라) PASS).**` |
| 163행 부근 | `> **구현 상태(2026-08-04, 실기 PASS):** 아래 규칙 17~22-1은 **코드로 구현되어 실기기 테스트를 통과했다.** 조준 조작은 탭 기반 2단계(...)로 구현되었고(기존 "누른 채 드래그(연속 홀드)" 방식은 폐기)` |

### 그런데 이 문서를 가리키는 4곳이 전부 「미구현」이라고 말한다

| # | 문서 | 위치 (특징 문구) | 적힌 내용 |
|---|---|---|---|
| 1 | `Assets/_Project/Docs/GameSystemRules.md` | 19행 부근 · 파일 목록 표의 `GameSystemRules_Skills.md` 행 끝 | `... 모바일 지점 조준 UX, 서버 권위 (기획 확정/미구현)` |
| 2 | `Assets/_Project/Docs/GameSystemRules.md` | 92행 부근 · 「스킬 건물 관련 작업」 빠른 참조의 마지막 줄 | `- 서버 권위(좌표만 RPC 전송 + 서버 재검증), 기획 확정 / 미구현` |
| 3 | `AGENTS.md` | 33행 부근 · 문서 인덱스의 `GameSystemRules_Skills.md` 행 끝 | `... 모바일 지점 조준 UX (기획 확정/미구현)` |
| 4 | `Assets/_Project/Docs/GameDesignDocument.md` | 203행 부근 | `> **구현 상태: 기획 확정 / 미구현** (구체 스킬 목록·수치는 추후 데이터로 확정). 현재는 배치·피격만 되는 시각 오브젝트 + 철거 버튼 공유 상태.` |

**4번이 특히 나쁘다.** 바로 윗줄(202행 부근)이 `> **상세 규칙은 단일 소스 문서 참조:** [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md)` 로 SSoT를 **정확히 가리키고 있는데, 그 다음 줄에서 낡은 상태를 단언한다.** 링크는 맞고 내용은 틀린, 검사기로는 절대 못 잡는 형태다.

**2번도 자체 모순을 하나 더 안고 있다.** 같은 절의 바로 위 줄이 조준 UX를 **「설계 정정: hold-drag → 탭 기반(코드 미반영, 후속 작업)」** 이라고 적는데, SSoT 163행은 **「탭 기반 2단계로 구현되어 실기 통과, 기존 홀드-드래그 방식은 폐기」** 다. 정면 충돌이다.

### 반대 근거 (구현 완료 쪽)

- `Assets/_Project/Docs/PROJECT_STATUS.md` · `Assets/_Project/Docs/ROADMAP.md` 둘 다 「**구현 완료 (2026-08-04)** 스킬 건물 시스템 Phase 1 … 실기기 테스트 PASS」 및 「**구현 완료 (2026-08-05)** 타입 C Phase 2 … 실기+멀티(클라) PASS」를 싣고 있다.
- `.claude/MEMORY.md` 「공통 중요 교훈」 절도 「스킬 메커니즘 3종(A/B/C) 전부 완료」라고 적는다.

→ **「구현 완료」쪽 근거는 SSoT 본문 + 상시 참조 3문서 + 공용 메모리로 여러 겹이고, 「미구현」쪽은 전부 인덱스·요약 문구다.** 어느 쪽이 옳은지는 명확하다.

### 왜 이것이 최우선인가

`WORKFLOW.md` [4] 는 이렇게 지시한다.

> **`Assets/_Project/Docs/GameSystemRules.md` 읽기 필수 — 예외 없음. Plan.md 작성 전 반드시 읽을 것.**
> 세부 규칙은 `Assets/_Project/Docs/GameSystemRules/` 하위 파일에 있다. 인덱스를 먼저 읽고 작업과 관련된 파일을 추가로 읽는다.

즉 **인덱스가 진입점으로 규정돼 있고, 그 진입점이 「미구현」이라고 말한다.** 다음 사람이 이미 구현·검증된 시스템을 미구현으로 알고 착수할 수 있다. `AGENTS.md` 인덱스(3번)도 `WORKFLOW.md` 「작업 시작 전 확인」이 지정한 진입점이라 같은 성격이다.

---

## 6. 🔴 [B] 에이전트 정의 4개가 「`Write` 금지」 규칙과 정면 충돌

### 확인된 사실

다음 4개 파일이 **자기 파일 안에서 자기모순**이다. 네 곳 모두 문구가 완전히 동일하다.

```
- Use the Write and Edit tools to update your memory files
```

| 파일 | 위치 |
|---|---|
| `.claude/agents/game-programmer.md` | 135행 |
| `.claude/agents/game-design-lead.md` | 113행 |
| `.claude/agents/project-orchestrator.md` | 98행 |
| `.claude/agents/qa-tester.md` | 143행 |

그런데 **같은 파일 아래쪽 「## MEMORY.md」 절**에는 이런 경고가 있다.

> ⚠️ **MEMORY.md 는 이미 내용이 있을 수 있다. 비어 있다고 가정하지 마라.**
> … `Write` 로 파일 전체를 다시 쓰면 앞선 세션이 쌓아 둔 지식이 통째로 사라진다 (2026-08-20 실제 사고)

### 범위를 정확히 말한다 (일반화 금지)

**6개 중 4개다.** `.claude/agents/asset-prompt-crafter.md` 와 `.claude/agents/document-manager.md` **2개에는 그 영문 줄이 없다** — 전수 검색으로 확인했다. 즉 **과거에 정리하다 만 부분 정리**이지, 「모든 에이전트 정의가 그렇다」가 아니다.

> 이 구분을 명시하는 이유: `.claude/mistakes.md` 「2026-08-24 한 사례를 확인하고 전체로 일반화」가 **확인한 범위와 결론의 범위가 달랐던** 사고다. 여기서는 6개 전부를 검색한 출력이 있으므로 "6개 중 4개"라고 쓸 수 있다.

### 왜 위험한가 — 자동 주입 경계 때문이다

`.claude/MEMORY.md` 「자동 주입 경계 (2026-08-21 프로브 실측)」 절이 확인한 사실:

| 층 | 자동 주입 |
|---|---|
| `CLAUDE.md` | ✅ 예 |
| 에이전트 정의 `.claude/agents/<이름>.md` | ✅ 예 — **단, 자기 것만** |
| `AGENTS.md` | ❌ |
| `.claude/MEMORY.md` | ❌ |
| 각 에이전트 `MEMORY.md` | ❌ |

즉 **틀린 도구를 안내하는 줄은 매 호출마다 자동으로 읽히고**, 그것을 반박하는 `Write` 금지 원문(`.claude/MEMORY.md` 「🔴 에이전트 메모리 갱신 규칙」 절)은 **`Read` 해야만 도달한다.** 충돌 시 자동으로 실리는 쪽이 이길 위험이 크다.

그리고 이것은 **`.claude/mistakes.md` 「2026-08-17 에이전트 메모리 -378행 소실 — 프롬프트에 하드코딩된 거짓 상태」의 재현 조건 그 자체**다. 그 사건은 프롬프트에 박힌 `Your MEMORY.md is currently empty.` 한 줄 때문에 에이전트가 `Edit` 대신 `Write` 를 골라 378행을 날린 사고였고, 교훈은 **「프롬프트에 상태를 사실로 못 박지 말고 절차로 적는다」** 였다. 지금 남아 있는 것은 상태 서술이 아니라 **도구 선택을 잘못 안내하는 줄**이라 모양이 조금 다르지만, 결과는 같다.

---

## 7. 🔴 [C] 이미 거짓으로 판명된 「200행」 기준이 3곳에 남아 있다

### 원본 규칙은 이 기준을 명시적으로 부정한다

`.claude/MEMORY.md` 「🔴 에이전트 메모리 갱신 규칙」 4번:

> **1차 기준은 성격이다** — 이유는 절삭이 아니라 **매 작업마다 읽는 파일이라 가벼워야 하기 때문**이다. (종전 "200행 초과분은 잘려서 안 보인다"는 **거짓으로 확인됐다**)
> **2차 기준은 경고선 250행.**

### 낡은 기준의 사본 3곳

| # | 위치 | 원문 (특징 문구) | 성격 |
|---|---|---|---|
| 1 | `.claude/agents/document-manager.md` 125행 | `각 에이전트의 MEMORY.md는 200줄 이내로 유지. 갱신 기준:` | **행동 지시** — 가장 위험 |
| 2 | `Assets/_Project/Docs/ROADMAP.md` 56행 · `✅ 완료 (2026-08-21, **정적 확인 — 규칙 신설**)` 행 | 규칙 4를 요약하며 `④ **200행 이내 인덱스 유지**` | 이력 요약 |
| 3 🆕 | `AGENTS.md` 104행 · 「에이전트 메모리」 절의 `**[🔴 2026-08-21 보강 …]**` 인용 블록 안 | `② **200행 절삭**: MEMORY.md 는 프롬프트에 실릴 때 **200행 뒤가 잘린다.** 초과분은 … 커밋 이력에도 안 남아 사고가 났다는 것조차 모른다.` | **손실 메커니즘 서술** |

> 🆕 **이번 조사에서 추가 확인.** 인계 목록에는 1·2번만 있었으나, 낡은 200행 기준을 전수 검색한 결과 `AGENTS.md` 에도 한 벌이 더 있었다. **인계 목록보다 넓다.**

### 3번이 왜 특히 나쁜가

`.claude/MEMORY.md` 는 이 항목을 **손실 위험이 아니라 품질 항목으로 재분류**했다.

> **품질 항목(손실 아님):** … 종전에는 이것을 "200행 초과 = 잘려서 안 보임"으로 적어 **손실 위험**으로 분류했으나 **2026-08-21 프로브로 그 전제가 거짓임이 확인됐다.** 잘리는 것이 아니라 **읽는 비용**의 문제이므로 **손실 방지가 아니라 품질 개선(오해 방지) 항목**이다.

그런데 `AGENTS.md` 104행은 여전히 이것을 **「손실 경로 3가지 중 하나」** 로 세고 있다. 숫자만 낡은 게 아니라 **분류 자체가 낡았다.**

### 실측 모순 — 지시를 따르면 손실이 난다

`document-manager` 자신의 `.claude/agent-memory/document-manager/MEMORY.md` 는 **228행**이다(실측). 1번 지시를 문자 그대로 따르면 **문서 담당 에이전트가 자기 메모리 28행을 지우려 들 수 있고**, 그것이 바로 갱신 규칙 3(「정리했다」·「간결하게 줄였다」는 삭제 사유가 못 된다)이 막으려는 행위다.

### 검사기는 이런 것을 못 잡는다

검사 항목 7종은 **규칙 번호와 마크다운 링크**만 본다. 문서 본문의 **수치 충돌**은 대상이 아니다 → [G]-3 참조.

---

## 8. 🟡 [D] 「구현 완료 후 QA 필수」 vs 「사용자 지시 없이 QA 금지」 — 3개 문서 충돌

| 문서 | 자동 주입 | 원문 (특징 문구) |
|---|---|---|
| `CLAUDE.md` 규칙 3 | ✅ **예** | `- 구현 후 검증 → **qa-tester** 에이전트` |
| `AGENTS.md` 126행 · 에이전트 역할 표 | ❌ | `| **qa-tester** | 구현 검증 / 버그 체크 | 구현 완료 후 반드시 | 변경된 파일 목록, 예상 동작 |` |
| `WORKFLOW.md` [5] | ❌ | `**TC 작성([5-1]) 및 QA 테스트([5-3])는 사용자가 명시적으로 지시한 경우에만 진행** — 먼저 제안하거나 묻는 것도 금지` |
| `WORKFLOW.md` ⚠️절대 금지 절 | ❌ | `**[5-1~5-3] TC 작성 및 QA 테스트는 사용자 명시적 지시 없이 진행 금지** — 사용자가 먼저 요청하지 않는 한 TC/QA 제안·진행 불가` |

**충돌 구도:** `CLAUDE.md`(항상 보임) + `AGENTS.md` 는 「반드시」, `WORKFLOW.md`(작업 사이클 단일 권위 소스) 는 「지시 없이는 금지」. **자동 주입되는 쪽이 정반대를 말한다** — [B] 와 같은 구조의 위험이다.

`AGENTS.md` 의 「구현 완료 후 반드시」는 특히 강한 표현이라, 인덱스만 본 사람은 QA를 건너뛰는 것을 규칙 위반으로 오해할 수 있다.

### `WORKFLOW.md` 내부에도 읽히는 모순이 하나 있다

사이클 다이어그램에서:

```
[5] 에이전트 위임 결정 → 컨텍스트 공유 → 구현
      ↓ ← 구현 완료 후 바로 [6]으로 이동 (TC/QA는 사용자가 명시적으로 지시한 경우에만 진행)
[5-1] Testcase.md 작성  ← 사용자가 명시적으로 요청한 경우에만 진행
...
[5-3] qa-tester 에이전트에게 테스트 요청 및 테스트 진행하여 문서에 반영  ← [5-1] 완료 즉시 진행 (이미 사용자 승인 완료)
```

`[5]` 화살표 주석은 「사용자가 명시적으로 지시한 경우에만」인데, `[5-3]` 주석은 「**[5-1] 완료 즉시 진행 (이미 사용자 승인 완료)**」이고, `[5-3]` 본문(단계별 상세 운영 규칙)은 조건 없이 `- qa-tester 에이전트에게 Testcase.md와 함께 테스트 요청` 이다.

「[5-1]에 들어갔다는 것 자체가 이미 승인이 있었다는 뜻」으로 읽어야 앞뒤가 맞지만, **그 전제가 문서에 적혀 있지 않다.** 지금은 독자가 추론으로 메워야 한다.

---

## 9. 🟡 [E] 존재하지 않는 문서를 여러 곳이 가리킨다

### 확인된 사실

`Assets/_Project/Docs/BuildAssetOptimizationReport.md` 는 **존재하지 않는다.**
- `Assets/_Project/Docs/` 전체 목록에 없다.
- 리포지토리 전체 `find` 결과 0건 (`.git` 제외).
- 유사한 이름은 `Assets/_Project/Scripts/Editor/AndroidBuildAssetOptimizer.cs` 뿐이며 이것은 코드 파일이다.

### 그것을 참조하는 곳 — 총 7곳

| 성격 | 위치 | 원문 (특징 문구) |
|---|---|---|
| **인덱스 등재** | `AGENTS.md` 62행 | `| `Assets/_Project/Docs/BuildAssetOptimizationReport.md` | 빌드 에셋 최적화 감사/중간 리포트 |` |
| **인덱스 등재** | `.claude/agents/document-manager.md` 86행 | 같은 행 (표를 복제한 것 — [J] 참조) |
| 🆕 상시 참조 안내 | `Assets/_Project/Docs/PROJECT_STATUS.md` 241행 · `Android AAB 용량 최적화 | ✅ 완료 (2026-07-15)` 행 끝 | `상세: `AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.` |
| 🆕 상시 참조 안내 | `Assets/_Project/Docs/ROADMAP.md` 290행 | `- **상세 문서**: `AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.` |
| 🆕 이력 기록 | `Assets/_Project/Docs/WORK_HISTORY.md` 38행 · `2026-07-15` 마일스톤 행 끝 | `상세 문서: `AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.` |
| 🆕 에이전트 메모리 | `.claude/agent-memory/game-programmer/work-history.md` 9행 | `**상세 문서**: `Assets/_Project/Docs/AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`` |
| 🆕 에이전트 메모리 | `.claude/agent-memory/project-orchestrator/project-history.md` 66행 | `- 상세 문서: `Assets/_Project/Docs/AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.` |

> 🆕 **이번 조사에서 추가 확인.** 인계 목록에는 **인덱스 2곳**만 있었으나, 전수 검색 결과 **총 7곳**이다. 성격이 서로 다르므로 [Plan] 에서 처리 방침도 갈라야 한다 — 인덱스 2곳은 "실재 문서 목록"이라 즉시 문제이고, 상시 참조 2곳은 독자를 없는 문서로 보내며, 이력·메모리 3곳은 **당시 기록**이라 성격 판단이 필요하다.

### 검사기 `[2]`(깨진 파일 링크)가 왜 못 잡았나 — 구조적 사각지대

`Tools/check_docs.py` 160행 부근의 링크 정규식:

```python
RE_LINK = re.compile(r"\[[^\]]*\]\(([^)#]+\.md)(#[^)]*)?\)")
```

**마크다운 링크 문법 `[텍스트](경로)` 만** 본다. 그런데 이 프로젝트의 인덱스는 경로를 **백틱 표기** `` `Assets/_Project/Docs/....md` `` 로 적는다.

전수 집계 결과:

| 표기 방식 | 건수 | 검사 `[2]` 대상 |
|---|---|---|
| 마크다운 링크 `[..](..)` | **99** | ✅ 검사됨 |
| 백틱 경로 `` `..` `` | **671** | ❌ 미검사 |

→ **검사 `[2]` 는 경로 참조의 약 13%만 덮는다.** 그리고 `AGENTS.md` 는 문서 인덱스인데 표 안이 전부 백틱이라 **인덱스 전체가 사실상 미검사 구간**이다.

---

## 10. 🟡 [F] 백틱 경로 전수 조사 — 실제로 어긋난 참조들

671건을 리포지토리 실제 파일과 접미사 매칭으로 대조하고, 브레이스 패턴(`{Blue|Red}` 등)·명령줄·플레이스홀더를 제외한 뒤 남은 것 중 **확인된 오류**다.

### (F-1) 경로가 틀린 것 — 파일은 있는데 위치가 다르다

| 문서에 적힌 경로 | 실제 경로 (find 확인) | 적힌 위치 |
|---|---|---|
| `Presentation/UI/Common/ToastKey.cs` | `Assets/_Project/Scripts/`**`Application/Events`**`/ToastKey.cs` | `.claude/agent-memory/game-programmer/work-history.md` 391행 |
| `Assets/_Project/Prefabs/`**`Misc`**`/GoldMineTile.prefab` | `Assets/_Project/Prefabs/`**`Buildings`**`/GoldMineTile.prefab` | `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` 73행 |
| `Assets/_Project/Prefabs/Units/`**`Unit_Pistoleer.prefab`** | `Assets/_Project/Prefabs/Units/`**`Human/Unit_Pistoleer_Blue.prefab`** · **`Unit_Pistoleer_Red.prefab`** | `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` 13행 · `.claude/agent-memory/project-orchestrator/project-history.md` 204행 |

**`ToastKey.cs` 가 특히 위험하다 — 레이어가 다르다.** 문서는 `Presentation`, 실제는 `Application` 이다. `.claude/MEMORY.md` 「아키텍처 핵심 제약」이 **레이어 경계를 어셈블리 정의 없이 네임스페이스 규약만으로 관리한다**고 명시하므로(`Assembly Definitions | 없음 — 네임스페이스 규약으로만 레이어 경계 관리`), 레이어를 틀리게 적은 메모리는 단순 오타보다 위험하다. 다음 사람이 그 파일을 Presentation 계층 것으로 알고 Presentation 규칙을 적용할 수 있다.

### (F-2) 「보존한다」고 적어 둔 파일이 실제로는 없다 — 사실 주장 불일치

`.claude/agent-memory/game-programmer/3d-transition.md` 의 「### 보존 파일 (사용하지 않지만 에셋 참조 때문에 유지)」 절:

- 적힌 내용: `Assets/_Project/Scripts/Infrastructure/Config/UnitAnimationData.cs` 를 「삭제하면 missing script 에러가 나므로 유지」
- **실측: 그 파일은 현재 리포지토리에 없다** (`find` 0건).

같은 문서의 「### 삭제 파일」에 적힌 `Presentation/Unit/FrameAnimator.cs` 도 `find` 0건인데, **이쪽은 기록과 실제가 일치한다.** 즉 **같은 문서 안에서 「삭제」 기록은 맞고 「보존」 기록은 틀렸다.**

> ⚠️ **여기까지가 확인된 사실이다.** 「보존하기로 한 파일이 나중에 지워졌다」는 것까지가 확인된 것이고, **왜·언제·누가 지웠는지는 확인하지 않았다.** 추정해서 적지 않는다 (CLAUDE.md 규칙 10 · `.claude/mistakes.md` 「검증 안 된 사실 주장을 여러 문서로 전파」).

### (F-3) 지금은 없는 과거 파일·폴더를 가리키는 이력 기록 (성격 판단 필요 — 자동 수정 금지)

**① 일회성 에디터 스크립트 7종**

`Assets/Editor/AIConfigSetup.cs` · `FixDifficultySelectViewLayout.cs` · `RemoveMissingScripts.cs` · `Assets/Editor/Setup/SetupInGameVolumePanel.cs` · `SetupLobbySettingsTab.cs` · `SetupNewUnitPrefabs.cs` · `Editor/SetupToastUI.cs`

`WORKFLOW.md` [5-2] 가 이렇게 허용한다.

> - 스크립트 실행 완료 후 해당 파일 삭제해도 무방 (1회성)

→ **이력 기록으로 남은 것은 규칙 위반이 아니다.** 다만 `Assets/_Project/Docs/PROJECT_STATUS.md` 260행 · `Assets/_Project/Docs/ROADMAP.md` 240행처럼 **상시 참조 문서**에 남은 것은 다음 사람이 실행하려다 못 찾는다.

**② 지금은 없는 `_Tasks` 폴더 5개**

`.claude/agent-memory/qa-tester/qa_history.md` 의 202 · 530 · 555 · 574 · 575행이 다음을 가리킨다.

- `2026-04-07/09_00_faction-ingame-apply`
- `2026-04-12/06_42_stats-apply`
- `2026-04-12/18_03_floating-hp-text`
- `2026-04-19/production-panel-rewrite`
- `2026-04-30/02_29_movement-combat-redesign`

**현재 `_Tasks/` 에는 `2026-04` 라는 단일 폴더만 있고, 날짜별 폴더는 `2026-05-06` 부터 시작한다.** 초기 폴더 구조가 나중에 바뀐 흔적으로 **보인다.**

> ⚠️ `WORKFLOW.md` 「규칙」 절은 `- 작업 폴더는 삭제하지 않음 (히스토리로 보존)` 이라 규정한다. 이 불일치가 **폴더 구조 변경의 흔적인지 실제 삭제인지는 확인하지 않았다** — 사용자 확인이 필요한 항목이다.

---

## 11. 🟡 [G] 검사기의 역할 수행 — 확인된 사각지대 3가지

`Tools/check_docs.py` 는 **선언한 7종 검사를 실제로 수행한다**(실행 출력 + 코드 확인). 도구가 고장 났다는 뜻이 아니다. 다만 **덮지 못하는 범위가 명확하다.**

### ① 백틱 경로 미검사
[E] 참조. 마크다운 링크 **99** : 백틱 경로 **671**. 인덱스 전체가 사각지대.

### ② `.claude/` 하위 문서가 검사 `[1]`~`[5]` 의 대상이 아니다

`main()` 790행 부근:

```python
ap.add_argument("--root", default="Assets/_Project/Docs")
```

그리고 `collect_files()` 는 거기에 리포지토리 루트의 `AGENTS.md` · `CLAUDE.md` **2개만** 추가한다.

→ `.claude/MEMORY.md` · `.claude/mistakes.md` · `.claude/agents/*.md` · `.claude/agent-memory/**` 는 **`[6]`·`[7]`(메모리 무결성)에만 걸리고, 규칙 번호·링크 검사에는 들어가지 않는다.**

실제로 `.claude/` 하위 문서에는 `GameSystemRules_*.md` 의 규칙 번호를 인용하는 줄이 **20건** 있는데 전부 미검사다. 예:
- `GameSystemRules_Buildings.md` 방어 타워 시스템 규칙 9
- `GameSystemRules_UI.md` 공통 UI 팝업 규칙 11
- `GameSystemRules_Skills.md` (1~26)

> **`--root` 를 돌려 쓰는 것으로는 해결되지 않는다.** 코드 790행 부근 주석이 **`--root` 를 바꾸면 검사 `[1]`·`[3]`·`[4]`·`[5]` 가 조용히 "이상 없음"이 된다**고 스스로 경고하고 있다.

### ③ 문서 본문의 사실·상태 충돌은 원리적으로 못 잡는다

검사기 docstring 「한계」 절이 이미 **「번호는 유효한데 엉뚱한 규칙을 가리키는 경우는 잡지 못한다」** 고 밝힌다.

[A]의 「구현 완료 ↔ 미구현」, [C]의 「200행 ↔ 250행」이 정확히 이 부류다. **이것은 도구의 결함이 아니라 도구가 할 수 있는 일의 경계다** — 사람/에이전트의 검토로만 잡힌다.

---

## 12. 🟡 [H] PROJECT_STATUS / ROADMAP / WORK_HISTORY 의 역할 경계 붕괴

### 규정된 역할 (`WORKFLOW.md` [11]②)

| 문서 | 규정된 역할 |
|---|---|
| `PROJECT_STATUS.md` | 완료된 시스템/버그 수정 항목 **갱신** |
| `ROADMAP.md` | 완료된 항목 **제거**, 다음 우선순위 조정 |
| `WORK_HISTORY.md` | 완료된 작업 마일스톤 **추가** |

### 실제 상태

**`ROADMAP.md` 는 완료 항목을 제거하지 않고 `✅ 완료` 로 계속 쌓고 있다.** 헤딩만 봐도:
- `### ✅ A-1 … 완료 (2026-05-24)`
- `### C-2 … ✅ 구현 완료 (2026-07-31)`
- `### F-1 … ✅ 완료 (2026-07-13)`
- `### F-2 … ✅ 완료 (2026-07-15)`

「우선순위 요약」 표도 `✅ 완료` 행이 다수다.

### 같은 서사가 세 문서에 거의 같은 문장으로 중복된다

| 사건 | 중복 위치 |
|---|---|
| 「구현 완료 (2026-08-20) 네트워크 종료 시점 가드 전수 보강 8곳(커밋 `bcf45ec1`)」 | `PROJECT_STATUS.md` 상단 · `ROADMAP.md` 상단 |
| 「구현 완료 (2026-08-05) 타입 C Phase 2」 | `PROJECT_STATUS.md` · `ROADMAP.md` |
| 「구현 완료 (2026-08-12) MistShrine」 | `PROJECT_STATUS.md` · `ROADMAP.md` |
| 로비 프로필/랭킹 클라우드 연동 (2026-07-16) | `PROJECT_STATUS.md` `## 2026-07-16 추가 완료: …` · `ROADMAP.md` `## 2026-07-16 로비 프로필/랭킹 클라우드 연동 상태` · **`.claude/MEMORY.md` `### 2026-07-16 - Current auth/profile state`** |
| 이메일 인증 플로우 보정 (2026-07-18) | `PROJECT_STATUS.md` `## 2026-07-18 완료: …` · `ROADMAP.md` `## 2026-07-18 … 완료` · **`.claude/MEMORY.md` `### 2026-07-18 - Email verification flow complete`** |

→ **한 사실이 3~4곳에 있다.**

### 왜 문제인가

한 사실을 3~4곳에서 갱신해야 하므로 **한 곳만 갱신되면 나머지가 낡은 사본이 된다.** [A] 가 정확히 그 결과다.

그리고 이것은 `.claude/MEMORY.md` 의 원칙을 정면으로 어긴다.

> **자동 주입되는 것은 다른 곳에 옮겨 적지 않는다** — 사본은 원본이 바뀌는 순간 조용히 거짓이 된다.

### 규모 참고 (사실)

| 문서 | 행수 | 용량 |
|---|---|---|
| `PROJECT_STATUS.md` | 951행 | 214KB |
| `ROADMAP.md` | 334행 | 126KB |
| `WORK_HISTORY.md` | 168행 | 204KB |

행수 대비 용량이 매우 크다 — **한 줄이 수천 자에 이르는 관습** 때문이다(`document-manager` 메모리에 「한 행이 매우 길다(수천 자) — 그게 이 파일의 관습이다」로 기록돼 있다).

---

## 13. 🟢 [I] 공용 메모리에 작업 이력이 섞여 있다 (갱신 규칙 4 위반)

`.claude/MEMORY.md` 는 갱신 규칙 4에 따라 **「인덱스 + 매 작업마다 필요한 것」만** 담아야 한다. 그런데 파일 하단에 작업 이력 3개 절이 **영문으로** 들어가 있다.

- `### 2026-07-16 - Current auth/profile state`
- `### 2026-07-16 - Email verification flow cleanup`
- `### 2026-07-18 - Email verification flow complete`

두 가지 문제가 겹친다.
1. **성격 위반** — 지나간 작업 기록은 토픽 파일로 빼야 한다(갱신 규칙 4).
2. **표기 불일치** — 파일의 나머지는 전부 한국어인데 이 3개 절만 영문이다.
3. **내용 중복** — [H] 표의 마지막 두 행이 이 절들이다.

특히 첫 절 제목의 **`Current`** 는 `document-manager` 메모리의 판단 기준(**「제목이 시간에 대해 거짓말하는가」**)에 정확히 걸린다. 2026-07-16 상태를 「Current」라고 부르고 있다.

---

## 14. 🟢 [J] `document-manager.md` 가 `AGENTS.md` 체크리스트를 통째로 복제

### 이미 어긋난 행이 있다

`.claude/agents/document-manager.md` 「### MEMORY.md 업데이트 (작업 완료 후)」 표는 `AGENTS.md` 「완료 후 업데이트 체크리스트」와 같은 표다. 그런데 **한 행이 이미 갈라졌다.**

| 문서 | 해당 행 |
|---|---|
| `AGENTS.md` | `문서 구조 변경 / **신규 에이전트 추가**` |
| `.claude/agents/document-manager.md` | `문서 구조 변경` (뒷부분 없음) |

**사본이 원본을 따라가지 못한 실례**다.

### 세 번째 사본도 있다

`.claude/agents/document-manager.md` 의 「담당 문서 전체 목록」 표는 다음의 **세 번째 사본**이다.
1. `AGENTS.md` 문서 인덱스
2. `.claude/MEMORY.md` 「주요 문서 경로」 표
3. `.claude/agents/document-manager.md` 표

그리고 없어진 `BuildAssetOptimizationReport.md` 를 1번과 3번이 **둘 다** 싣고 있는 것이 [E] 다.

### 참조로 바꿀 때 따져야 할 것

「사본을 만들지 않는다」 원칙대로면 **참조로 바꾸는 것이 맞다.** 다만 `AGENTS.md` 는 자동 주입되지 않으므로 **「참조로 바꾸면 도달 못 하는 것 아닌가」** 를 함께 따져야 한다.

→ **도달은 성립한다.** `.claude/MEMORY.md` 6행 부근이 이렇게 보장한다.

> 그리고 **여기서부터** 문서 인덱스 `AGENTS.md`(→ 아래 「주요 문서 경로」)와 각자의 `.claude/agent-memory/<이름>/MEMORY.md`(…)로 이어진다. **이 파일이 그 두 곳으로 가는 분기점이다.**

그리고 각 에이전트 정의는 「작업 시작 전 반드시 `Read` 할 것」으로 `.claude/MEMORY.md` 를 지정한다. 즉 **에이전트 정의(자동 주입) → `.claude/MEMORY.md` → `AGENTS.md`** 경로가 끊기지 않는다.

---

## 15. 🟢 [K] `CLAUDE.md` 마지막 줄의 번호 참조가 모호하다

`CLAUDE.md` 맨 끝 줄:

```
**[7]~[12] 중 하나라도 빠지면 사이클 미완료.**
```

이 문서 안에는 번호 체계가 **두 개** 있다.

| 체계 | 내용 |
|---|---|
| ① 「작업 시작 전 필수 체크리스트」 | `[ 1 ]` `[ 2 ]` `[ 3 ]` — **7~12가 없다** |
| ② 규칙 번호 | `## 1` ~ `## 14` — **7~12가 실재한다** |

`[7]~[12]` 가 `WORKFLOW.md` 의 **사이클 단계** [7]~[12](테스트 완료 확인 → 변경 파일 리스트업)를 뜻한다는 말이 **`CLAUDE.md` 어디에도 없다.** 바로 위 세 줄이 `WORKFLOW.md` 를 언급하긴 하지만, 번호가 그쪽 것이라는 명시는 없다.

`CLAUDE.md` 는 **자동 주입되는 유일한 문서**라 오독 비용이 가장 크다.

> ※ 참고 — 확인한 사실: `CLAUDE.md` 규칙 번호 1~14 는 **결번 없이 연속**이다. 그리고 검사기 `[4]`(섹션명이 없어 특정 불가한 참조)는 `GameSystemRules_*.md` 만 대상이라 이 모호성은 검사 범위 밖이다.

---

## 16. 🟢 [L] `CONTEXT.md` 는 인덱스에만 있고 갱신 경로가 없다

### 확인된 사실

`CONTEXT.md`(리포지토리 루트, 도메인 용어집, **69행**)는 `AGENTS.md` 15행에 등재돼 있다.

```
| `CONTEXT.md` | 프로젝트 핵심 도메인 용어집 | ❌ 수동 |
```

그런데 **어느 갱신 목록에도 없다.**
- `WORKFLOW.md` [11]② 갱신 대상 목록(PROJECT_STATUS / ROADMAP / WORK_HISTORY / GDD / TDD / GameSystemRules) — **없음**
- `AGENTS.md` 「완료 후 업데이트 체크리스트」 — **없음**

→ **누구도 갱신 책임을 지지 않는다.**

### 언급 위치는 3곳뿐

검토 범위(외부 스킬 폴더 제외) 안에서 `CONTEXT.md` 를 언급하는 문서는 전수 검색 결과 **3곳**이다.

| 위치 | 성격 |
|---|---|
| `AGENTS.md` 15행 | 인덱스 등재 |
| `Assets/_Project/Docs/Skills/SKILLS_GUIDE.md` 135행 | **외부 스킬(`grill-with-docs`)의 동작 설명** — 이 프로젝트의 갱신 규칙이 아니다 |
| `Assets/_Project/Docs/WORK_HISTORY.md` 32행 | 2026-07-19 신규 작성 기록 |

> ※ `.claude/skills/` · `.agents/skills/` 하위에도 다수 언급이 있으나 **외부 스킬 문서라 검토 범위 밖**이다.

### 내용도 한정적이다

실제로 열어 보면 **무작위 맵 도메인 용어에 한정**돼 있다 — 「대전 맵」·「맵 유형」·「완전개방형」·「장애물 개방형」 등. 유닛·전투·스킬·연구 등 다른 도메인 용어는 **없다.**

---

## 17. ⚪ [M] `_baseline.json` 의 `change_log` 서술과 현재 기준값이 어긋나 보인다 — **확인 필요**

### 관측된 것

`.claude/agent-memory/_baseline.json` 의 2026-08-24 `change_log` 항목은 이렇게 적는다.

- 증가: `document-manager 131→178`
- 감소 승인: `qa-tester 756 → 754행 (-2행)`

그런데 같은 파일의 `folders` 현재 값은:

| 폴더 | change_log 서술 | 현재 `folders` |
|---|---|---|
| document-manager | 178 | **228** |
| qa-tester | 754 | **812** |

그리고 `measured_at` 은 **`"2026-08-24"`** 로 change_log 항목과 같은 날짜다.

### 확인한 메커니즘 (사실)

`Tools/check_docs.py` 전체에서 **`measured_at` 을 쓰는 코드가 0건**이다(전수 검색). 즉 `--update-baseline` 은 `folders` 만 다시 쓰고 **`measured_at` 과 `change_log` 는 건드리지 않는다.**

이 사실은 `Assets/_Project/Docs/ROADMAP.md` 51행 부근에 이미 등록된 항목과도 일치한다.

> `🟢 낮음 (**미착수** — 2026-08-24 등록) | **`_baseline.json` 의 `measured_at` 을 `--update-baseline` 이 갱신하지 않는다** — 갱신 시 `folders` 만 바뀌고 `measured_at` 은 그대로라, **다음 갱신 후에는 "언제 실측한 값인가"가 거짓이 된다.**`

### 판단 — 단정하지 않는다

> **가능한 설명은 「같은 날 이후의 증가분을 반영한 재갱신」이고, 그렇다면 이것은 도구의 정상 동작이다.** `folders` 실측값은 [4절] 대조에서 **현재 폴더와 완전히 일치**하므로 기준값 자체는 정확하다.
>
> ⚠️ **「기록이 틀렸다」고 단정하지 않는다.** 어느 시점에 어떤 이유로 재갱신됐는지는 **확인하지 않았다.** `.claude/mistakes.md` 「2026-08-24 검증 안 된 사실 주장을 여러 문서로 전파」가 정확히 이 부류의 실수다 — 확인할 방법이 없으면 **"미확정"으로 표시**한다.
>
> 다만 `measured_at` 이 갱신되지 않는다는 것이 확인된 이상, **날짜만으로는 언제 잰 값인지 구분할 수 없다**는 사실은 확정이다. 사용자 확인 사항으로 넘긴다.

---

## 18. ⚪ [N] 이상 없음으로 확인된 것

**"검사했으나 문제가 없었다"도 결과다.** 다음 항목은 실제로 확인했고 문제가 없었다.

| # | 항목 | 확인 방법 | 결과 |
|---|---|---|---|
| 1 | `.claude/mistakes.md` 목차 ↔ 본문 | 목차 9건과 본문 H2 9건을 뽑아 diff | **제목까지 완전 일치, 차이 0** |
| 2 | 마크다운 링크 | 검토 범위 안 **99건** 전부 실제 파일과 대조 | **깨진 링크 0건** |
| 3 | `AGENTS.md` 인덱스 누락 | `Assets/_Project/Docs/` 아래 상시 참조 `.md` 를 인덱스와 대조 | **누락 0건** (반대 방향 오류인 [E] 만 존재) |
| 4 | 죽은 윈도우 절대경로 | `C:\Users\...` / `C:/Users/...` / `d:/Dmain/...` 를 md·py·json 전체에서 검색 | **검토 범위 안 잔존 0건** — 잔존은 `Build_BackUpThisFolder_ButDontShipItWithYourGame/` 빌드 산출물뿐으로 문서가 아니다 |
| 5 | 검사 `[6]`·`[7]` 동작 | 검사기 실행 + `_baseline.json` 6개 폴더 값 대 실측 | **정상 작동 · 6개 전부 일치** |

> **4번의 의미:** `.claude/mistakes.md` 「2026-08-20 죽은 윈도우 경로를 4회 놓침」 항목이 **실제로 해소됐음을 확인한 것**이다. 그 사건은 네 번에 걸쳐 잔존을 놓친 기록이라, "이번엔 정말 0건"을 전수 명령의 출력으로 확인해 두는 것에 의미가 있다.

---

## 19. 이 조사에서 확인하지 않은 것 (범위 밖 · 미확정)

추정으로 메우지 않기 위해 **확인하지 않은 것을 명시**한다.

| 항목 | 왜 확인하지 않았나 |
|---|---|
| [F-2] `UnitAnimationData.cs` 가 **왜·언제·누가** 삭제됐는가 | 파일이 없다는 사실만 확인했다. 경위는 git 이력에 있을 수 있으나 **git 명령은 CLAUDE.md 규칙 5로 금지**다 |
| [F-3] `_Tasks` 폴더 5개가 **폴더 구조 변경의 흔적인지 실제 삭제인지** | 현재 상태만 확인 가능하다. 사용자 확인 사항 |
| [M] `_baseline.json` 재갱신 시점·경위 | 도구가 `measured_at` 을 쓰지 않는다는 것까지만 확인됐다 |
| 코드가 문서와 일치하는가 | **이번 작업은 문서 검토다.** 코드 검증은 범위 밖(CLAUDE.md 규칙 6) |
| `_Tasks/` · `_Logs/` 내부 | `WORKFLOW.md` [11]③ 이 검사 대상 제외 · 소급 수정 금지로 규정 |
| 외부 스킬 문서 | 이 프로젝트가 관리하는 문서가 아니다 |


---

## 20. 🔴 2026-08-25 정정 — 원문은 그대로 두고 덧붙인다

이 문서는 조사 시점의 기록이므로 위 본문은 고치지 않는다. 이후 사용자와의 논의·추가 확인에서
**틀렸다고 판정된 것**과 **새로 확인된 것**만 여기에 적는다.

### 정정 1 — [G] 「검사기 사각지대 3가지」는 과한 규정이었다

`Tools/check_docs.py` 상단이 밝히는 목적은 두 가지뿐이다 — **① 규칙 번호 참조 정합성**,
**② 에이전트 메모리 무결성(검사 [6]·[7])**. 모든 문서를 검사하라고 만든 도구가 아니다.

| 종전 기재 | 판정 |
|---|---|
| ① 링크가 아닌 글자 경로를 검사하지 않는다 | **철회** — 도구 목적 밖이다. 다만 검사 이름이 「깨진 파일 링크」라 오해를 부르므로 **이름·설명을 고칠 여지**는 남는다 |
| ② `.claude/` 하위가 검사 [1]~[5] 대상이 아니다 | **유지** — `.claude/` 문서가 `GameSystemRules` 규칙 번호를 20건 인용하는데 미검사다. 도구가 선언한 목적 ① 안쪽의 빈틈이다 |
| ③ 문서 내용의 사실 충돌을 못 잡는다 | **철회** — 애초에 도구가 할 일이 아니다 |

### 정정 2 — [A] 스킬 시스템은 **코드로 판정**했다

문서 간 충돌은 실제 코드 구현을 근거로 통일한다는 방침에 따라 실물을 셌다.

- 스킬 관련 **19개 파일** 존재
- 타입 3종 실행기 전부 있음 — `InstantAreaDamageExecutor.cs`(A) · `AreaDotDamageExecutor.cs`(B) · `GlobalStatusChangeExecutor.cs`(C)
- 상태효과 `Application/Services/StatusEffectSystem.cs` + `Domain/Status/` 3파일
- UI·조준 `BuildingSkillPanelUI.cs` · `SkillCooldownOverlay.cs` · `SkillAimController.cs`(548행) · `SkillAimReticle.cs` · `Shaders/SkillAimOverlay.shader`
- 네트워크 `Infrastructure/Network/NetworkSkillController.cs`
- `SkillAimController.cs` 3행 주석이 **「탭 기반 2단계」** 로 구현됐음을 명시

→ **정본은 `GameSystemRules_Skills.md`(구현 완료)이고, 「미구현」이라 적은 인덱스 쪽이 거짓이다.**
「hold-drag → 탭 기반(코드 미반영)」도 코드와 어긋난다.

### 정정 3 — [C] 낡은 「200행」은 3곳이 아니라 **5곳**이었다

인계된 3곳 외에 `document-manager/MEMORY.md` 안에 2곳이 더 있었다(손실 경로 절의 ② 항목,
「200행 인덱스 원칙」). 전수 검색으로 확인했다.

### 새로 확인된 것 — 관리 규칙 사본은 **4곳에 14벌**이었다

| 위치 | 벌 수 |
|---|---|
| `.claude/MEMORY.md` 「갱신 규칙」 | 1 (원본) |
| `.claude/agents/*.md` 의 `# Persistent Agent Memory` 블록 | 6 (2벌은 이미 갈라짐) |
| `.claude/agent-memory/*/MEMORY.md` 상단 경고문 | 7 (5개 파일) |
| `AGENTS.md` 인용 블록 | 1 |

에이전트 정의 4개(`game-programmer`·`game-design-lead`·`project-orchestrator`·`qa-tester`)의
블록은 **폴더 경로 2곳만 빼면 글자까지 동일**했고, `document-manager`(30행)·`asset-prompt-crafter`(24행)는
같은 틀의 짧은 판본이었다 — 즉 사본이 이미 벌어져 있었다.

### 새로 확인된 것 — `AGENTS.md` 가 자기 자동 로드 상태를 잘못 적고 있다

`AGENTS.md` 표는 자신을 `✅ 항상`(자동 로드)으로 적지만, 이번 세션에서 자동 주입된 것은
`CLAUDE.md` 하나뿐이었다. `.claude/MEMORY.md` 「자동 주입 경계」 표(❌)가 맞다.
설정을 직접 확인한 결과 **`.claude/MEMORY.md`·`AGENTS.md` 를 실어주는 장치가 존재하지 않는다**
(`settings.json` 에 import 없음, `CLAUDE.md` 에 `@경로` 구문 없음).

### 조사에서 걷어낸 오판 — [F] 189행은 문제가 아니었다

`document-manager/MEMORY.md` 의 「자동 주입되는 문서에는 포인터만」은 `.claude/MEMORY.md` 가 아니라
**`CLAUDE.md`** 를 가리키는 문장이었다(다음 줄이 "`CLAUDE.md` 같은 상시 로드 문서"라고 명시). 정상이다.
