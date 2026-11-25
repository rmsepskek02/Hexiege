# 물결의 가호 후속 정리 — Research

## 무엇을 왜 하나요

물결의 가호(정령 지원 스킬) 작업을 끝내면서, **사실과 맞지 않게 남은 글귀 네 군데**가 발견됐습니다. 게임 동작은 하나도 바꾸지 않고 글귀만 지금 사실에 맞춥니다.

1. **자가 검증 실패 메시지 한 줄** — 검사가 실패할 때 보여 주는 영어 문장에 「옛 줄은 주석으로만 남아 있다」고 적혀 있는데, 그 주석 줄은 물결의 가호 정리 때 이미 지웠습니다. 검사 동작은 그대로 두고 문장만 고칩니다.
2. **전투 표현 스키마 숫자** — 두 앱(Host · Client)이 같은 통신 약속을 쓰는지 확인하는 번호(스키마)가 2026-11-25 최적화 통합 병합에서 12 → 13이 됐는데, 스킬 규칙 문서 두 곳과 상태 문서 두 곳이 아직 「지금 12」라고 적고 있습니다.
3. **셋업 도구 재실행 기록** — 상태 문서 연막탄 행에 「셋업 도구를 다시 실행한 기록이 없다」고 적혀 있지만, 2026-11-26 물결의 가호 테스트에서 사용자가 셋업 도구를 다시 실행했고 「사용 가능 스킬 15개」를 확인했습니다.
4. **지원 스킬 수치 정리 위치** — 스탯 문서(StatsReference)에는 지원 스킬 수치 절이 없습니다. 지금 만들지 않고 **밸런싱 작업 때 만든다**는 결정을 로드맵 밸런싱 행에 적어 둡니다.

- 사용자 지시(원문, 2026-11-26): 「다 추천대로 정리해서 병합푸시도해」 → 「task 문서 작성하고 바로 구현진행해」.
- 출처: [물결의 가호 Task Plan 11절](../../2026-11-25/18_35_support-skill-tide-blessing/Plan.md#11-final-result) 마무리 때 문서 관리 에이전트가 범위 밖으로 보고한 항목(스키마 도장)과 메인 세션 추천안.
- 확인 기준: 브랜치 `claude/vfx-code-followups` HEAD `fa632b4` (이 문서는 읽기만 했다 — 코드 · 다른 문서 수정 없음).
- 수정 방법 · 근거 · 검증은 [Plan](Plan.md).

> **상태 (2026-11-26): Research 작성 — 구현 전.**

---

## 1. 항목별 현재 상태 (모두 직접 확인)

### 1-1. 자가 검증 실패 메시지 (코드 문자열)

| 위치 | 지금 글귀 | 사실 |
|---|---|---|
| `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.SupportRegen.cs` **632행** (메인 세션 전달 「약 634행」 — 실제 632행) | `"TB-T2 the Elemental Resonance placeholder row must be out of the setup table (kept only as a comment) — Plan T-1."` | 셋업 도구 `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs`에 원소 공명 자리표시 `InstantPlaceholder("Skill_Spirit_ElementalResonance", …)` 주석 줄이 **없다**. `fa632b4`에서 두 줄(`// InstantPlaceholder(…`, `//     "기능 기획 보류 중인 스킬이다(이름만 등록).", allies: false),`)이 지워졌다(`git show fa632b4 -- …SupportSkillLoadoutSetup.cs`). 지금 그 파일의 `ElementalResonance` 언급은 45행 설명 주석 하나뿐이다. |

- **검사 동작:** 632행 바로 앞 줄 `Require(SupportActiveLineCount(setup, "InstantPlaceholder(\"Skill_Spirit_ElementalResonance\"") == 0, …)` (631행) — 주석이 아닌 줄만 센다. 629행 주석 「주석 줄은 세지 않는다 … 나중에 지워도 결과가 같다」와 35행 머리 주석 「주석이 아닌 줄에 원소 공명 자리표시 0」은 **지금도 사실**이다. 틀린 것은 실패 메시지 괄호 안 「(kept only as a comment)」 하나다.
- 이 문장은 검사가 **실패할 때만** 출력된다. 지금은 통과하므로 화면 · 로그에 나오지 않는다 — 고쳐도 동작 · 출력이 바뀌지 않는다.
- `fa632b4`가 이 파일에서 바꾼 것은 R-T9 옛 검사 주석 두 줄 삭제뿐이라(`git show fa632b4 -- …SupportRegen.cs`) 632행 메시지는 그때 함께 고쳐지지 않았다.

### 1-2. 낡은 스키마 숫자 도장

**현재 코드:** `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` **55행** `public const int PresentationContractSchemaRevision = 13;` (54행 「이전 값: … = 12;」, 52행 「[2026-11-25 통합] main 연막 회피(12)에 HP RPC의 DoT 출처 bool을 합쳐 13.」). 바꾼 커밋은 병합 `0ebe909`(2026-11-25, 「Merge latest main with all pending optimizations, log menu and research UI」 — `git show 0ebe909 -- …UnitAttackShadowProfileResolver.cs`에서 12 → 13 확인). `NetworkGameFlow.cs` 83행이 이 상수를 읽는다.
네트워크 TDD는 이미 13이다: `TechnicalDesignDocument_Network.md` 42행 「**현재 전체 통합 프로토콜:** `PresentationContractSchemaRevision` = **13**」(「서버 권위 투사체 전송 계약」 절).

| 문서 · 위치 | 지금 글귀(그 부분) | 성격 |
|---|---|---|
| `GameSystemRules/GameSystemRules_Skills.md` 「멀티플레이 / 서버 권위」 규칙 25 — 362행(재생의 뿌리 · 회복 동기화 항목) | `(그때 10 — 11은 아래 공습 항목, 지금 12는 아래 연막탄 항목) *(2026-11-24 개정 — 직전 「지금 11은 아래 공습 항목」)*` | 상태 도장(「지금」) — 틀림 |
| 같은 규칙 25 — 367행(메테오 항목) | `**스키마는 그때 11 그대로**였다(지금 12는 아래 연막탄 항목) *(2026-11-24 개정 — 직전 「스키마는 11 그대로다」)*` | 상태 도장 — 틀림 |
| `PROJECT_STATUS.md` 29행(지원 스킬 강화 상태 VFX 행) | `*(이 작업 기준 — 지금 스키마는 연막탄 행의 12 · 2026-11-24 갱신, 직전 「공습 행의 11」 · 2026-11-18 갱신, 직전 「단계 C 행의 10」 · 그 전 「지속 상태 구역 행의 9」)*` | 상태 도장 — 틀림 |
| `PROJECT_STATUS.md` 33행(재생의 뿌리 행) | `*(이 작업 기준 — 지금 12, 아래 연막탄 행 · 2026-11-24 갱신, 직전 「지금 11, 아래 공습 행」)*` | 상태 도장 — 틀림 |

- 규칙 25 369행 「전투 표현 스키마가 **11 → 12**가 됐다」(연막탄)와 PROJECT_STATUS 36행 연막탄 행의 「스키마 11 → 12」 · 「스키마 12」(그 경기 로그 값)는 **그때의 기록**이라 맞다 — 고치지 않는다.
- PROJECT_STATUS 37행(물결의 가호 행)은 이미 「스키마 13 그대로」다.

### 1-3. 셋업 도구 재실행 기록 (PROJECT_STATUS 연막탄 행)

| 위치 | 지금 글귀 | 사실 |
|---|---|---|
| `PROJECT_STATUS.md` 36행(연막탄 행) 「알려진 한계」 칸 끝 | `남은 주석 비활성화 블록: 셋업 도구 옛 `PersistentZone` 머리 · `TargetsAllies` 줄(셋업 도구 재실행 기록 없음).` | 2026-11-26 물결의 가호 테스트에서 사용자가 셋업 도구 메뉴 `Hexiege/Setup/Skills/Create Support Skill Assets And Replace Loadout`를 다시 실행했다 — [물결의 가호 Plan 11절](../../2026-11-25/18_35_support-skill-tide-blessing/Plan.md#11-final-result) TB-L0 「PASS — 셋업 도구 「사용 가능 스킬 15개」 — 사용자 보고. 「값이 바뀐 자산 0 · 새로 만든 자산 1」 숫자는 전달된 판정에 없음」, TB-L15 회귀 스위트 PASS(사용자 「다 PASS였어」). |

- 이 「재실행 기록 없음」은 연막탄 Task Plan 412행 「남겼다: 셋업 도구 옛 `PersistentZone` 머리 · `TargetsAllies` 줄(셋업 도구를 다시 실행한 기록이 없어 대체 실행 미확인)」에서 왔다. 그 Task 문서는 이력이라 고치지 않는다(WORKFLOW [11]).
- 두 주석 블록은 지금도 남아 있다: `SupportSkillLoadoutSetup.cs` 267~269행(「옛 머리(아군 인자 없음 — 대상이 항상 적):」 + `// private static SkillSpec PersistentZone(…` 두 줄) · 281행(`// 옛 줄(대상 항상 적): … TargetsAllies = false,`).
- **이번 범위:** 상태 문서의 「재실행 기록 없음」 글귀만 사실로 고친다. 두 주석 블록을 지울지는 범위 밖이다(1-5 질문 Q-1).
- 같은 「재실행 기록 없음」 글귀는 `Docs/` · `AGENTS.md` · `CLAUDE.md` · `.claude/` 관리 문서에서 PROJECT_STATUS 36행 하나뿐이다(이력 Task 제외, `grep -rna` 확인). 비슷한 뜻의 다른 줄은 1-5에 적는다.

### 1-4. 지원 스킬 수치 절 (StatsReference)

- `StatsReference.md`에는 지원 스킬 수치 절이 **없다** — 절 목록(`grep -n "^#"`)은 자원 · 범위 공격 · 유닛 3종족 · 건물 3종족 · 연구소뿐이다. 물결의 가호 Plan 11절 표(325행)도 「StatsReference — 반영 안 함 — 이 문서에는 지원 스킬 수치 절이 없다(앞 14개 스킬도 없음). 임시값의 원본은 스킬 규칙 13 「수치」」로 적었다.
- 지금 지원 스킬 임시값이 적힌 곳: 셋업 도구 값 표(`SupportSkillLoadoutSetup.cs` `BuildSpecs()` — 자산을 만드는 실제 값) · 스킬 규칙의 메커니즘별 수치 문장(규칙 13 「수치」 = 강화 · 쉴드 · 재생 계열, 규칙 11 메테오 수치 160행 등). 모두 「시험용 임시값 — 밸런싱 단계에서 확정」이다.
- **ROADMAP 밸런싱 행:** `ROADMAP.md` 「기획·경제·콘텐츠·UI」 표 55행 `| 추후 별건 | 밸런싱+AI 시나리오 통합 재검토. 현재 값은 [Stats](StatsReference.md)만 참조하고 이곳에 값/DPS/쿨다운 사본을 두지 않음 |`. 같은 표 61행(「중간·기획 미확정」 — 스킬 건물 데이터 결정, 「확정 스킬 15개의 수치는 임시값이며 밸런싱 확정이 아님」)도 밸런싱을 말하지만, 밸런싱 작업 자체의 행은 55행이다. Plan은 55행에 적는다(Plan 2-4 — 사용자 확인 대상으로 표시).
- 55행은 「이곳에 값 사본을 두지 않음」이라 ROADMAP에 숫자를 옮기지 않는다.

### 1-5. 이번 범위 밖으로 발견한 것 (보고만 — 고치지 않음, CLAUDE.md 규칙 6)

| # | 위치 | 내용 | 메모 |
|---|---|---|---|
| F-1 | `TechnicalDesignDocument_Network.md` 26행 「회복 동기화 재사용(지원 스킬 재생의 뿌리)」 단락 | `(그때 10 — 11은 아래 「경기 시작 호환 확인」의 공습 방향 인자, 지금 12는 같은 곳의 연막탄 회피 칸)` — 1-2와 같은 낡은 「지금 12」 도장. 같은 문서 42행은 이미 13 | 메인 세션 범위 목록에 없다. 함께 고칠지 사용자 확인 필요(Q-2) |
| F-2 | `.claude/agent-memory/game-programmer/skill-system.md` 227행 | 「setup tool's old Sacred Tree `InstantPlaceholder` line (tool rerun not evidenced)」 — 셋업 도구 재실행 증거가 없다는 도장. 1-3과 같은 사실(2026-11-26 재실행)로 낡았을 수 있음. 그 줄이 가리키는 주석 줄이 지금 남아 있는지는 확인하지 않았다 | 메모리 갱신은 WORKFLOW [9] 단계 몫. 보고만 |
| F-3 | `SupportSkillLoadoutSetup.cs` 267~269 · 281행 | 1-3의 두 주석 블록이 남은 이유(재실행 미확인)가 이제 해소됐다 — WORKFLOW [4] 「기존 로직 제거 규칙」의 최종 삭제 조건을 다시 볼 수 있다 | 삭제 여부는 사용자 결정(Q-1) |

### 1-6. 사용자 · 메인 세션 확인이 필요한 질문

| # | 질문 | 이 문서의 처리 |
|---|---|---|
| Q-1 | 연막탄이 남긴 셋업 도구 주석 블록 2곳(F-3)을 이번에 지울지 | 이번 범위에 넣지 않았다 — Plan에도 없음 |
| Q-2 | 네트워크 TDD 26행 「지금 12」(F-1)를 이번에 함께 고칠지 | 이번 범위에 넣지 않았다 — Plan 「하지 않는 것」에 적음 |
| Q-3 | ROADMAP 기록 위치가 55행(밸런싱+AI 시나리오 통합 재검토)이 맞는지 — 61행(스킬 데이터 결정)일 수도 있음 | Plan은 55행으로 적음 |

---

## 2. 영향 범위

- **게임 동작 · 빌드 · 통신:** 없음. 코드 변경은 실패할 때만 출력되는 문자열 한 줄(에디터 전용 어셈블리 `Assets/Editor/`)이다. 런타임 · 네트워크 코드는 건드리지 않는다 — 스키마 상수도 그대로 13.
- **자가 검증:** TB-T2의 판정 조건(`SupportActiveLineCount` 비교)은 그대로다. 메시지 문자열을 검사하는 다른 검사가 있는지 — `grep -rn "kept only as a comment" Assets/Editor`로 632행 하나만 있음을 확인했다.
- **문서:** 스킬 규칙(규칙 25 두 곳) · PROJECT_STATUS(3행) · ROADMAP(1행). 규칙 번호 · 절 이름 · 앵커는 바뀌지 않는다.

## 3. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-26 | 최초 작성 (HEAD `fa632b4`) |
