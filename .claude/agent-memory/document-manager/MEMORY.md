# document-manager 누적 지식

## 2026-08-24 B3 역할교대 FAIL 문서 판정
- self-validation/계측 완전성 PASS와 실기 전체 판정을 분리한다. MOVE terminal FAIL 세션의 read-only 회전 대조는 원인 분리 근거이지 공식 RootCrossAudit 판정이 아니다.
- 현행 수치: Android Host endpoint/evidence 54/54, Editor Client 53/53, overlap 48, 같은 revision 회전 잔차 7(`0.11~0.15°`), Unit 30 adapter failure 6·repeated recoverable 1.
- 다음 문서 갱신은 Unit 30 교정과 새 역할교대 공식 CrossAudit 뒤에 수행하며, 그 전 B3는 FAIL/OPEN으로 유지한다.

## 이 프로젝트의 문서 관습 (실측으로 확인된 것만)

### 원문 보존 방침
- 기존 서술을 **지우지 않는다.** `~~취소선~~` + `> **[이전 기록 — YYYY-MM-DD 갱신 전]**` 인용 블록으로 남기고 현행을 덧붙인다.
- `LogRules.md` 는 문서 상단에 **개정 이력 표**를 두고 **시간 오름차순**으로 행을 추가한다(맨 아래가 최신). 순서 주의.

### 상시 참조 3문서의 갱신 형태
| 문서 | 형태 |
|---|---|
| `PROJECT_STATUS.md` | 상단 `**최종 수정일:**` 갱신 + 그 아래에 `**구현 완료 (날짜) — 검증 상태:**` 문단을 **맨 앞에 prepend**. `**현재 단계:**` 문단 맨 앞에도 한 줄 추가 |
| `ROADMAP.md` | 상단 문단 prepend + **우선순위 요약 표**의 해당 행을 `🔴 높음 (미착수)` → `✅ 완료 (날짜, 검증상태)` 로 교체. 새로 생긴 잔여 항목은 표에 행 추가 |
| `WORK_HISTORY.md` | **마일스톤 표**(날짜 역순)의 맨 위에 행 1개 추가. 한 행이 매우 길다(수천 자) — 그게 이 파일의 관습이다 |

### 과대 표기 금지 (CLAUDE.md 규칙 10)가 문서에 나타나는 형태
- 완료 항목 옆에 **`⚠️ 과대 표기 금지 — 아직 완료가 아닌 것:`** 목록을 반드시 병기한다.
- "컴파일 통과"와 "실기 검증 PASS"는 **명확히 구분**해서 적는다. 확인받지 않은 커밋은 "통과했다"고 쓰지 않는다.
- 사용자가 범위 밖으로 결정한 항목은 **"미해결 결함이 아니라 범위 밖 확정 항목"** 이라고 명시한다.

### 규칙 번호
- **절대 재배열·신설하지 않는다.** 코드 주석과 과거 Task 문서가 번호를 참조한다.
- 해석을 명문화할 때는 **새 번호 대신 기존 규칙 본문에 문장을 추가**한다.
- `GameSystemRules_UI.md` · `GameSystemRules_Buildings.md` 는 섹션마다 번호가 1부터 반복 → 참조 시 **섹션명(H2) 병기 필수**.

## Task Plan.md 사후 갱신 패턴
계획 본문(§1~§10)은 **원문 그대로 두고**, 문서 끝에 `# 11. 구현 결과 (날짜 추가)` 절을 append 한다.
포함 항목: 자연어 요약 / 실적표 / 계획과 달라진 점 / **⚠️ 미완 단서** / **변경 파일 리스트업**(WORKFLOW [12]).

## 도구
- `python3 Tools/check_docs.py` — 리포지토리 루트에서 실행. 읽기 전용. **0건 확인 후 보고.**
- **git 명령 금지**(규칙 5). 변경 파일 목록은 `grep`/코드 실측으로 재구성한다.

## 자주 쓰는 실측 명령 (숫자를 문서에 적기 전 반드시 재확인)
```
grep -rn "Debug\.Log" Assets/_Project/Scripts --include=*.cs | wc -l     # 잔존(주석 포함)
grep -rn "LogEvent\.Unknown" Assets/_Project/Scripts --include=*.cs      # 0건 유지 확인
```
`GameLog` 호출은 **여러 줄에 걸치는 경우가 있어** 한 줄 정규식 grep 으로 세면 누락된다 →
파이썬으로 `GameLog\.(Ops|Dev)\.(Info|Warn|Error)\s*\(` 매치 후 뒤쪽 300자에서 인자를 읽어야 정확하다.

- 톤: 모든 프로젝트 문서는 **한국어 + 굵게 강조 + 근거 파일/라인/커밋 해시 명기**. 추정 표현 금지(CLAUDE.md 규칙 10).
- "과대 표기 금지" 관례 — 미검증 항목은 반드시 "미검증"/"보류"/"범위 밖"으로 라벨링한다.

### 2026-08-23 - Unit ActionSequence B3 v9 코드 게이트 문서 동기화

- v8 Android recoverable preflight 반복 뒤 v9 typed 후보/preflight, 동일 probe/final-stage seam, staged accounting과 lifecycle retire 보강이 구현됐다. Runtime/Editor Roslyn PASS, 독립 정적 QA P0~P2 없음.
- `planned`는 실제 commit attempt 직전, `committed`는 success 뒤에만 증가하고 END equality를 강제한다. recoverable history는 positive successful spatial commit에서만 clear한다.
- 후속으로 pre-merge Unity 메뉴 self-validation은 PASS했다. main 통합 뒤 재실행과 Android v9는 미실행이므로 B3는 `FAIL / OPEN`. `spatialFinalRecoverableRepaths`는 0 강제가 아니라 resolved/nonrepeat/no-cleanup coverage다. 영구 규칙과 Testcase는 변경하지 않는다.

### 2026-08-23 - B3 v9/main 문서 conflict 통합

- HEAD의 B3 v9 이력과 main의 `GameLog`·씬 무관 로그·종료 안전 이력은 모두 보존한다. pre-merge Unity self-validation PASS는 기록하되 통합 뒤 compile/self-validation/최종 QA와 Android v9 전까지 B3 `FAIL / OPEN`이다.
- 신규 증거는 Editor `_Logs/_editor/{date}/RuntimeLog.txt`, device `_Logs/{date}/{HH_mm}_logcat/RuntimeLog_device*.txt`; match 직전 buffer clear/종료 뒤 save, `Role=Host/Client + sharedSessionKey + schema` 결합, ambiguity fail-closed를 따른다.

### 2026-08-23 - B3 v9 최종 통합 정적 코드 게이트 동기화

- `origin/main` 60개 commit과 B3 v9 의미 병합 완료, unmerged 0·staged 최신 해결본 일치. main `GameLog`·`LogSessionOwner`·combat shutdown·`IsSpawned`·research 수정과 B3 이력을 모두 보존한다.
- post-merge Runtime/Editor Roslyn PASS와 독립 QA P0~P3 0만 코드 게이트 PASS로 기록한다. pre-merge `[UAS-DIAG]` PASS를 post-merge 결과로 재사용하지 않는다.
- 감사 계약은 최신 device anchor + Editor daily, role/shared key/production schema exact/전체 BEGIN·END identity/EVIDENCE·FAIL/source gate다. Android-safe terminal 26+5+1=32줄·최대 968 UTF-8 byte와 manifest 27개/초과 길이 fail-closed, adapter stateful 64/65·release `Conditional` 경계를 보존한다.
- 이 코드 게이트 기록 당시에는 post-merge 메뉴와 Android v9가 미실행이었다. 후속 실제 경기와 RootCrossAudit false-INCONCLUSIVE 교정 상태는 아래 항목으로 갱신하며, 공식 재감사 전 B3는 `FAIL / OPEN`이다. 영구 Unit 규칙은 계약 변화가 없어 수정하지 않는다.

### 2026-08-23 - RootCrossAudit false-INCONCLUSIVE 문서화

- 실제 `a0e690...8d8` Editor Host·Android Client는 MOVE full EVIDENCE·ROOT PASS, 수동 pose 49/49 match다. ROOT bucket 14/17 cross-peer equality가 false-INCONCLUSIVE를 만들었고 MOVE `startedAt` delta는 `.733149초`다.
- 진단기만 ROOT peer bucket equality 제거 + run 내부 invariant 유지 + validated MOVE `startedAt <=2.000초`로 교정했다. `2.000/2.001`, stable overlap, fail-closed, Play Mode 차단과 read/`IOException` INCONCLUSIVE를 함께 기록한다.
- Editor Roslyn PASS와 사용자 Unity SelfValidate/Analyze 미실행을 분리한다. 공식 실제 재감사는 OPEN이며 게임·서버 권위·로그 포맷·영구 규칙 변경 없음.

### 2026-08-20 - Unit ActionSequence B3 v8 코드 게이트 문서 동기화

- 완료 표현은 “v8 구현·Runtime/Editor Roslyn·독립 QA 코드 게이트 PASS”로 제한한다. Unity 메뉴 self-validation과 Android 실기가 미실행이므로 B3 전체는 `FAIL / OPEN`이다.
- 불변식은 `checkpoint 소비 ≠ 공간 타일 도착`이다. sampled Root 경계 전이 전체를 preflight한 뒤 staged `Prepare → publish → commit`으로 원자 반영하고, ReducerAuthoritative 직접 `ProcessStep`은 금지하며 Legacy rollback은 보존한다.
- 규칙 문서는 이미 이 계약으로 정정돼 추가 변경하지 않았다. Task Research/Plan, Log Round 8, 상태 3문서와 관련 MEMORY만 동기화하고 Testcase는 생성하지 않는다.

---

## Testcase.md 규칙 (자주 헷갈림)
- WORKFLOW.md [5-1]: **사용자가 명시적으로 지시했을 때만 작성.** 먼저 제안하는 것도 금지.
- 따라서 문서 갱신 요청을 받아도, TC를 지시받은 적 없으면 **Testcase.md를 새로 만들지 않는다.**
  대신 Plan/Research 하단 결과 섹션에 "Testcase 미작성 — 사용자 미지시" 사유를 남긴다.

- Completion update pattern: append task `Plan.md` completion result, add PASS results to task `Testcase.md`, update `PROJECT_STATUS.md`/`ROADMAP.md` progress sections from in-progress to complete, and replace the `WORK_HISTORY.md` in-progress entry with completed device verification results.
- Keep stale unverified account cleanup as a long-term policy item, not part of the completed client flow slice.

### 2026-07-20 - 유닛 전투 규칙 문서 구조 개정

- `GameSystemRules_Units.md`는 게임플레이 불변 조건, 신규 `GameSystemRules_UnitCombatSynchronization.md`는 멀티플레이 복제·시간·순서 계약, 신규 `Assets/UnitCombatAssetMatrix.md`는 25종 구현·에셋 감사 상태를 담당한다.
- `GameSystemRules.md`, `AGENTS.md`, `CONTEXT.md`, TDD, StatsReference, PROJECT_STATUS, ROADMAP, WORK_HISTORY와 작업 Research/Plan을 한 배치로 동기화했다.
- 과거 완료 기록은 삭제하지 않고 Legacy 이력으로 보존하되 현재 상태 표에서는 v2 재검증으로 명시한다. 문서 설계 완료와 런타임 완료를 혼동하지 않는다.
- 신규 문서 링크와 변경 문서의 로컬 Markdown 링크를 검사했고 `git diff --check`를 통과했다.

### 2026-07-22 - main 반영 후 유닛 전투 문서 재동기화

- InfernoSpirit·QuakeSpirit의 Legacy 구현 사실과 규칙 v2 완성도를 반드시 분리한다. 피해 기능/로그 PASS를 ActionSequence·표현 동기화 Complete로 승격하지 않는다.
- QuakeSpirit 스탯은 25번째 항목으로 추가됐지만 기본 Attack marker는 여전히 없고 1.00초 값은 placeholder다. Inferno는 marker 0.50초/설정 1.15초 불일치가 남는다.
- 상태 문서의 오래된 “Quake/Inferno 미구현”, “Quake UnitStats 누락” 문구를 현재 상태로 교정하고 과거 기록은 날짜가 있는 Legacy 이력으로만 보존한다.

### 2026-07-27 - Unit ActionSequence A2 완료 문서 동기화

- A2 완료는 “서버 권위 pose 관측 seam PASS”로만 기록하고 이동/공격/Impact 세 증상 해결이나 v2 권위 전환 완료로 확대하지 않는다.
- 로그 종료 당시 Impact 전 in-flight 회차는 누락으로 세지 않는다. 이번 Host 수치는 schedule 429 / dispatch 428, in-flight 1이며 완료 회차 누락·중복 0이다.
- 규칙·TDD의 계약 변경은 없었다. 기존 서버 권위와 Legacy writer/emitter를 유지한 구현·검증 상태 변경이므로 Task Research/Plan, PROJECT_STATUS, ROADMAP, WORK_HISTORY와 관련 에이전트 MEMORY만 갱신했다. 사용자 요청으로 생성된 Testcase.md가 없어 새로 만들지 않았다.

### 2026-07-27 - Unit ActionSequence B0 완료 문서 동기화

- B0는 “read-only Visual Root migration readiness PASS”로만 기록한다. 50개 프리팹의 실제 migration, Presentation seam, writer 전환이나 세 동기화 증상 해결 완료로 확대하지 않는다.
- 결정성 근거는 연속 두 dry-run의 50/50, errors 0, assetsModified 0과 동일 aggregate manifest SHA-256이다. 최초 불안정 해시는 임시 native bookkeeping을 포함한 진단기 문제였고 NGO 의미 설정 allowlist로 교정했다.
- 계약·게임플레이 수치 변경이 없어 GameSystemRules/TDD/GDD는 무수정이다. Task Research/Plan, PROJECT_STATUS, ROADMAP, WORK_HISTORY와 game-programmer/project-orchestrator/qa-tester/document-manager MEMORY를 갱신했다. Testcase는 사용자 요청이 없어 생성하지 않았다.
- 다음 상태는 B1 50개 프리팹 원자적 migration이며, 완료 문구에는 NetworkObject/NetworkTransform 식별자 보존·부분 저장 0·rollback·2회차 diff 0 검증 전까지 “Root 분리 완료”를 쓰지 않는다.

### 2026-07-27 - Unit ActionSequence B1 문서 동기화

- 2026-07-27 당시 B1은 `asset migration + journal completed + Apply 재실행 NO-OP`까지만 PASS로 기록했다. 후속 rollback 판정은 아래 2026-07-29 항목을 따른다.
- 신규·교체 프리팹 영구 계약은 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-ROOT-004`, 실무 체크리스트는 `Assets/UnitCombatAssetMatrix.md`, 구현 검증은 `ValidateUnitCombatSetup.cs`가 담당한다.
- 기존 50개용 `SetupUnitVisualRoots.Apply`는 일회성 migration이다. 신규 프리팹은 migrated 템플릿으로 만들고 UnitType/Blue·Red pair/Matrix/validator 고정 roster·종족·VFX 기준선을 함께 갱신한다.
- 현행 validator 계약에는 파일명, direct identity VisualRoot, root projector, root Animator/Renderer 0, Animator Root Motion off와 Animator별 동일 GO relay가 포함된다. Collider는 B1 검증 범위가 아니다.
- ROADMAP의 과거 `SetupNewUnitPrefabs.cs` 참조는 실제 파일이 없으므로 Legacy 이력으로 표시하고 현행 authoring 권위를 명시한다.

### 2026-07-29 - Unit ActionSequence B1 rollback 문서 동기화

- B1 rollback은 graceful index 0/24/49와 crash index 24 별도 복구를 구분해 기록한다. graceful 공통 근거는 `JournalRecovered=true`, `VerifiedFileCount=100`, `InitialAnalyzerPassed=true`이고 crash는 복구 전 mismatch/VisualRoot/projector 25건과 복구 후 100/100을 함께 남긴다.
- primary Unity compile Tundra success와 `[UAS-DIAG]` self-validation PASS는 보조 검증으로 기록한다.
- Android 1대와 Unity Editor counterpart의 역할교대 Host/Client `[UAS-ROOT-POSE]` runtime smoke와 Blue/Red 교차 감사가 pending이므로 B1 overall은 OPEN, B2 시작 금지 문구를 유지한다.
- Task Plan, PROJECT_STATUS, ROADMAP, WORK_HISTORY와 project-orchestrator/game-programmer/qa-tester/document-manager MEMORY를 동기화했다. 규칙·코드·프리팹 계약은 변경하지 않았다.

### 2026-07-29 - Unit ActionSequence B1 Collider 가정 제거 재교정

- 2026-07-27 B1에서 근거 없이 추가된 Collider 존재·배치 가정은 B1 범위가 아니다. optional/root-only 같은 대체 계약도 만들지 않고 Rule/TDD/AssetMatrix/상태 문서와 MEMORY에서 제거한다.
- B1 권위는 network component placement, identity VisualRoot, root Animator/Renderer 0, projector/ref, Root Motion off, Animator별 relay, 서버 single-writer와 client Simulation Root write 금지다.
- 최종 실기 권위는 Android 1대와 같은 코드 리비전의 Unity Editor counterpart가 역할교대하는 두 경기다. Match A는 Editor Host Blue file + Android Client Red Logcat, Match B는 Android Host Blue Logcat + Editor Client Red file을 짝지어 분석한다.
- 각 경기에서 sharedSessionKey 동일·available, role/isFlipped, 180초 내 Blue/Red·2 types·2 moved units·3초 stable, 양쪽 END coverage PASS/errors 0과 외부 stable cross-audit를 확인한다. Windows/Standalone build는 사용하지 않으며 Android 2대는 release E2E·성능·호환성 권장 항목이지 B1 필수는 아니다.

### 2026-07-30 - Unit ActionSequence B1 최종 문서 동기화

- Match A/B 역할교대 root-pose 교차 감사에서 pose mismatch 0과 양쪽 로컬 PASS를 확인해 B1을 COMPLETE로 전환했다. 다음 구현 게이트는 B2 서버 이동·SimulationFacing Shadow다.
- 규칙·TDD·Asset Matrix의 현행 NetworkTransform 계약은 서버 권위/canonical world-space, `Interpolate=true`, `PositionLerpSmoothing=false`다. 전체 보간 비활성화로 기록하지 않는다.
- B1 완료는 Simulation/Visual Root, migration·멱등성·rollback과 안정 pose 수렴 범위다. 공격 방향·Impact/피해 시점·result seam·권위 전환·25종 멀티 QA는 미완료다.
---

## 문서 정합성 검사기 `Tools/check_docs.py` (2026-08-13 등록)
- 문서 수정을 마치면 리포지토리 루트에서 `python3 Tools/check_docs.py` 실행 → **0건 확인 후 보고**. 읽기 전용이라 문서를 고치지 않는다. 이 스크립트 자체는 수정 대상이 아니다.
- 검사 5종: 규칙 번호 결번 / 깨진 파일 링크 / 실재하지 않는 규칙 번호 / **섹션명 없는 모호한 참조** / 괄호 병기 내용과 규칙 제목 불일치.
- `GameSystemRules_UI.md`·`GameSystemRules_Buildings.md`는 **섹션마다 규칙 번호가 1부터 반복**된다. 참조할 때는 **H2 제목 전체**를 그대로 붙여야 검사 [4]를 통과한다
  (`MistShrine 규칙 21` ✗ → `MistShrine 물안개 힐 시스템 규칙 21` ✓ / `랠리포인트 규칙 2` ✗ → `랠리포인트 시스템 규칙 2` ✓).
- **규칙 번호 자체는 절대 재배열 금지** — 코드 주석 519곳·과거 Task 문서 1,102곳이 참조 중. 고칠 대상은 항상 참조하는 쪽.
- 검사 [4]는 같은 줄에 섹션명이 있기만 하면 통과하므로 **엉뚱한 섹션명도 통과한다.** 통과가 목적이 아니라 정확한 섹션 지정이 목적 — 문맥으로 확정 못 하면 손대지 말고 보고(CLAUDE.md 규칙 10).
- 등록 위치: `CLAUDE.md`(문서 시스템 절) · `WORKFLOW.md` [11]-③ · `AGENTS.md`(작업 사이클 표) · `.claude/agents/document-manager.md`(담당 표 + "문서 정합성 검사" 절 + 완료 체크리스트).

---

## GameSystemRules 문서 수정 판단 기준
- 버그 수정이 **기존 규칙을 코드가 다시 준수하도록 맞춘 것**이면 → **규칙 문서 변경 불필요.**
  대신 상태 문서(PROJECT_STATUS/WORK_HISTORY)에 "규칙 문서 변경 없음(코드가 규칙을 다시 준수)"를 명시해 다음 사람이 재검토하지 않게 한다.
- 규칙 자체가 없거나 틀렸을 때만 규칙 신설/수정 → 이는 **설계 결정**이므로 사용자 승인 필요(CLAUDE.md 규칙 6·12). 임의로 새 규칙 번호를 추가하지 말 것.

---

## 알려진 문서 간 정합성 함정
- `AGENTS.md` 체크리스트는 "항상 PROJECT_STATUS/ROADMAP/WORK_HISTORY"를 요구하지만,
  호출 에이전트가 전달하는 대상 목록에서 `.claude/MEMORY.md`(공용)가 누락되는 경우가 있다.
  → 범위 밖 문서는 임의로 고치지 말고 **최종 보고에서 누락 가능성을 지적**한다.
- `.claude/MEMORY.md`와 project-orchestrator MEMORY의 경로 표기는 Windows 절대경로(`d:/Dmain/...`)로 되어 있다.
  실제 작업 환경 경로와 다를 수 있으나 **사용자 로컬 기준 표기이므로 임의 수정 금지.**
- game-programmer `MEMORY.md`는 이미 200줄을 넘어섰다(2026-08-08 기준 300줄+).
  새 항목은 **"최근 작업" 맨 위에 5줄 이내로** 넣고, 상세는 토픽 파일(`gameplay-systems.md` 등)로 보낼 것.
  토픽 파일에 이미 "구조적 취약점" 목록이 있으면 **중복 서술 대신 해당 항목을 취소선+"수정 완료"로 갱신**한다.

---

## 작업 이력(문서 관점)
- 2026-08-08 랠리포인트 BlockingOverlay 잔존 버그 — 문서 갱신 7건 수행. 규칙 문서(UI 규칙 5 / 건물 랠리 규칙 2)는 변경 불필요로 판정.
  후속 제안(미반영): UI 규칙 5에 "조준 모드 진입으로 팝업만 숨기는 경로는 `HideBlockingOverlay()` 짝 호출 필수 + `Close()` 우회 경로는 참조 카운터 직접 반납" 문장 추가 — 동일 결함이 스킬 패널·랠리 2회 발생했으므로 규칙 명문화 가치 있음(사용자 승인 필요).
## 인계받은 수치가 실측과 어긋난 사례 (2026-08-18)
- 인계 메모의 **`system` 문자열 분포**(Network 163 등, 합 262)가 실측(Network 273 · Auth 46 · Bootstrap 26 · UI 22 · Cloud 11 · Factory 7 · Audio 4 · HexGrid 1 · Input 1, 합 391)과 **달랐다.**
  → **문서에 옮겨 적지 않고 사용자에게 보고**했다. 인계 수치는 항상 재실측한다.
- 인계 메모의 *"클래스명이 계획과 달라졌다"* 는 항목은 Plan 이 언급한 클래스 13종을 전수 조회해도 **특정할 수 없었다** → 추정하지 않고 그대로 두고 보고.
