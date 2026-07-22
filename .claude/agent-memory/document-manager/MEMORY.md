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

## 업데이트 패턴 (발견 사항)
- game-programmer 메모리는 `MEMORY.md`(200줄 요약, "최근 작업" 섹션에 최신 항목 prepend) + `work-history.md`(상세 전체) + 주제별 토픽 파일(network.md/architecture.md/ui-system.md 등)로 분리. 코드 변경 시: ① MEMORY.md 최근 작업에 요약 추가 ② 해당 토픽 파일에 상세/교훈 추가.
- 버그 수정 시 토픽 파일이 이미 존재할 수 있음(예: random-matching-bugfix.md는 별개 2026-03 버그). 새 버그는 관련 토픽 파일(network.md 등) 본문에 추가하고, 기존 동명 파일과 혼동 주의.
- PROJECT_STATUS.md / ROADMAP.md / WORK_HISTORY.md는 모두 헤더에 "최종 수정일" + "현재 단계" 보유 → 작업 완료 시 3개 모두 갱신.
- 표준 완료 작업 항목 형식: 시스템 분류 표(`| 항목 | 상태 | 비고 |`)에 행 추가. WORK_HISTORY는 마일스톤 표 상단에 날짜 역순 prepend.

## 누적 교훈
- 환경 주의: 작업 환경은 세션마다 Windows/원격 경로가 달라질 수 있음. 현재 프로젝트 루트는 세션의 `cwd`/workspace root를 기준으로 확인하고, 문서에는 기존 Windows 경로(`d:/Dmain/...`) 표기가 남아 있을 수 있음을 감안한다.
- PROJECT_STATUS.md는 길어서(약 680줄) Read가 truncate됨 → 부분 read한 줄은 Edit 전 해당 위치를 다시 Read해야 편집 가능. 헤더(날짜/단계)는 첫 5줄만 다시 읽으면 됨.
- 아키텍처 리팩토링 문서화 위치 (2026-06-26 IUnitFactory 작업 기준):
  - TechnicalDesignDocument.md: "Clean Architecture 구조" 섹션 아래 "의존성 방향 추상화(Application 인터페이스 패턴)" 서브섹션 + 맨 끝 "변경 이력" 표.
  - PROJECT_STATUS.md: 코드 정리/리팩토링 항목들이 모인 표(클린업 Phase 1/2 근처)에 행 추가.
  - ROADMAP.md: "우선순위 요약" 표 + 헤더 날짜/단계.
  - WORK_HISTORY.md: "마일스톤 이력" 표 최상단(날짜 역순)에 행 추가.
- 의존성 역전 패턴(인터페이스=Application, 구현=Infrastructure) 사례 모음: IGameServices, IUnitFactory, IEntityPositionProvider, IForfeitService.
- 사운드 시스템 규칙 문서는 `GameSystemRules/GameSystemRules_Sound.md`에 있음(규칙 번호제). 버그 수정이 기존 규칙의 누락을 드러내면(예: 2026-07-08 BUG-1 — 규칙 8이 StopCoroutine만 기술) WORKFLOW [12] 근거로 해당 규칙에 요건을 보완할 것.
- Task 문서(Research/Plan)는 히스토리 보존이 원칙 → 본문을 재작성하지 말고 하단에 "완료 결과"/"실제 구현 결과" 섹션을 append하여 계획 대비 달라진 점(예: 폰트 Light→Bold, BUG-3 원인 추정 오류)을 기록.
- 실기 버그 수정 작업 완료 시 갱신 대상: PROJECT_STATUS(헤더+시스템 표 행 추가), ROADMAP(헤더+우선순위 표), WORK_HISTORY(마일스톤 prepend), game-programmer MEMORY(최근 작업 prepend), qa-tester MEMORY(해당 시스템 QA 섹션에 실기 결과 반영), 공용 .claude/MEMORY.md(교훈), 관련 GameSystemRules. 사용자 MEMORY(Windows 경로)는 Linux 환경에서 접근 불가 → 갱신 불가함을 사용자에게 알릴 것.
- game-programmer MEMORY "최근 작업"에 **이미 해당 작업 항목이 존재하나 "실기 테스트 대기 중" 상태로 남아 있을 수 있음**(구현 세션에서 선반영). 이 경우 새 항목 prepend가 아니라 **기존 항목을 in-place로 갱신**(대기 중→PASS, 발견 버그·교훈 추가)하는 것이 중복을 막는다. prepend 전에 기존 항목 존재 여부부터 확인할 것.
- "완료 결과" append 위치: 히스토리 보존을 위해 각 task 폴더의 **Plan.md 하단**에 append(Research는 원상태 유지). 계획대로 된 항목 / 계획과 달라진 점(범위 확장·반복 수정) / 실기 확인 결과를 구분해 기록. TC/QA를 사용자가 명시적으로 요청하지 않았으면 Testcase.md는 만들지 말고 "완료 결과"로 대체(그 취지를 한 줄 명시).
- GameSystemRules에 "미정 노트"가 달린 규칙(예: Sound 규칙 26 음소거 내부 구현)은, 해당 작업이 그 방식을 **확정하고 실기 통과하면** 노트를 삭제하고 확정 내용을 규칙(신규 규칙 번호 부여 가능)으로 보완하는 것이 WORKFLOW [12] 후속 반영. Plan.md의 "규칙 문서 후속 반영" 섹션에 이 지시가 미리 적혀 있을 수 있으니 확인.
- Docs 트리에 GameSystemRules_Sound.md는 `GameSystemRules/` 하위(규칙 번호제, 현재 규칙 27까지). GameSystemRules_UI.md에 인게임 프로필 서브패널(규칙 6)·로비 ProfilePanel/SettingPanel 분리 섹션 존재.
- 신설 규칙 초안이 Plan.md에 "[U-17]" 식으로 적혀 있으면, 검증 완료 후 정식 등재. **기존 파일의 마지막 규칙 번호를 반드시 Read로 확인하고 이어지는 번호를 부여**할 것. 2026-07-12 전투 타이밍 동기화: GameSystemRules_Units.md는 규칙 16까지였고 → 규칙 17~21을 "전투 연출 동기화 규칙" 새 섹션(전투 연계 규칙 뒤)으로 추가. GameSystemRules_Buildings.md 방어 타워 시스템은 규칙 11까지였고 → 규칙 12(타워 발사 연출) 추가. 규칙 간 상호 참조(B-12가 Units 규칙 19 큐를 참조 등)를 문안에 명시하면 정합성 유지.
- 완료 섹션 append 대상은 원칙상 Plan.md만이나, **작업 지시가 Research/Plan 둘 다에 명시하면 지시 우선**(2026-07-12는 양쪽 append). Research엔 이미 검증 로그 절(7·8절)이 누적돼 있을 수 있어 다음 번호 절(9절 "최종 검증 및 완료")로 이어붙임.
- TDD 전투 이벤트 문서 위치: "이벤트 기반 전투 통신" 섹션(약 850행, EntityAttackedEvent/OnUnitDied/OnBuildingDied 코드블록). 전투 구조 변경(EntityDamagedEvent에 공격자 Id 추가, HitPresentationQueue 신설)은 이 섹션 바로 뒤에 서브섹션으로 최소 추가. **GDD에는 전투 연출/피격 기술이 없음** → 전투 타이밍/연출 작업은 GDD 무수정("불일치 없으면 건드리지 않음" 준수).
- 전투 파이프라인 핵심 파일/개념(2026-07-12 기준): 데미지=서버 권위(NetworkCombatController.TickCombat 50ms 격자), 타격 시점 HitFrameTimes=Attack 클립 OnAttackHit에서 UnitFactory 자동 추출, 피격 연출=HitPresentationQueue(공격자 로컬 OnAttackHit에 동기화), EffectManager.PlayUnitHit/PlayBuildingAttack + UnitEffectConfig.hitPreset/tracerPreset + BuildingEffectConfig.attackPreset. UnitEffectView.cs는 삭제됨(더는 참조 금지).
- 이동/Walk 애니메이션 동기화(2026-07-13, task `_Tasks/2026-07-12/07_55_movement-walk-anim-sync`) 갱신 대상 실사례 — Research/Plan **둘 다**에 "최종 검증 및 완료" 절 append(Research=다음 번호 절 7절, Plan=하단 신규 섹션). Units.md 규칙 22 등재(규칙 21 뒤, "애니메이션 상태 동기화 규칙" 새 서브섹션 + 규칙 21에 상위 대체 참고 노트 추가). PROJECT_STATUS(헤더+시스템 표 유닛 이동 근처 행 추가), ROADMAP(헤더+우선순위 표 행 완료 전환+Phase F-1 완료 처리+신규 F-5 Firebase/EDM 저장소 방침 추가), WORK_HISTORY(마일스톤 prepend), TDD("유닛 애니메이션 상태 동기화" 서브섹션을 피격 표현 큐 서브섹션 뒤에 추가 + 변경 이력 표 0.21.0 행 — 참고: combat-timing(0.20.x)은 변경 이력 표에 누락돼 있었음).
- BloomFairy 힐러(2026-07-18, task `_Tasks/2026-07-18/03_40_bloomfairy-healer`): GameSystemRules_Units 규칙 31 뒤에 "힐러 확장" 섹션으로 규칙 32~36 추가(힐러 전용 경로/부상 아군 탐색/HoT·DoT 공용 시스템/힐러 유휴 감시 `HealerIdleWatchV3`/쿨다운 예외). **쿨다운 예외**가 핵심 — BloomFairy만 `AttackCooldown`(3.0s)이 힐 발동 준비(1.0s, `HitFrameTimes[0]`)를 미포함해 실제 주기 4.0s(다른 유닛은 `TryAttack` 패턴으로 쿨다운=전체 주기). 의도된 설계이므로 규칙 36 + StatsReference BloomFairy 행 비고 양쪽에 "되돌리지 말 것" 명문화(향후 버그 오인 방지). Research/Plan 둘 다 하단에 "완료 결과/QA 반영" append(이슈1=설계확정·코드무변경, 이슈2=유휴감시 수정, 이슈3=CastHeal `Hp<MaxHp` 가드).
- "엣지 트리거 RPC → NetworkVariable 레벨 동기화" 전환 서사(비개발자 설명 재사용 가능): 상태처럼 "현재 값 자체가 의미"인 것은 1회성 신호(엣지)가 아니라 값 공유(레벨)로 다뤄야 스폰 레이스/신호 유실에 구조적으로 안전. 계측 잔여 수치(41건)가 정상 동작(우회)을 오탐할 수 있어 무조건 버그로 보지 않고 성격 확인 후 코드 무수정 종결 — 이 "오탐 종결"을 문서에 지표 한계로 명시하는 패턴.
- Plan 대비 변경 기록 시 자주 나오는 형태: 비활성화 예정이던 코드가 실제로는 다른 기능 가드였음(`_combatAnimationSent`=데미지/타겟 RPC 게이팅) → 제거하지 않고 유지, 그 사유를 규칙 문안·완료 섹션 양쪽에 명시.
- 한 세션에 독립적인 소규모 작업 여러 건(예: 2026-07-13 죽은 코드 제거+Animator 상태 의존 제거+Firebase 게이트 제거)을 문서화할 때: PROJECT_STATUS/WORK_HISTORY는 **묶어서 1개 dated 서브섹션/마일스톤**으로 기록(각 건은 개별 행/불릿으로 구분), ROADMAP 우선순위 표는 **건별 ✅ 완료 행**으로 분리. game-programmer/qa/orchestrator MEMORY도 묶음 1개 항목으로 prepend. task 폴더가 있는 건(anim-resume)만 해당 Plan.md에 "완료 결과" append(Research 원상태 유지). 신규 규칙이 없는 순수 리팩토링/버그수정은 GameSystemRules·TDD·GDD 무수정(불일치 없으면 건드리지 않음).
- 로컬 임포트 대형 SDK를 `#if SYMBOL` 게이트로 감싸면 심볼 미정의 시 스텁이 조용히 대체돼 기능 무조건 실패 — 이 교훈은 game-programmer/qa MEMORY + AuthSystemRules("기술 구성" 절의 SDK 저장소 방침 노트) + ROADMAP F-5에 분산 기록. AuthSystemRules는 로그인 SDK 의존 문서라 SDK 저장소/게이트 방침 노트를 "기술 구성" 절에 두는 것이 적절.
- 원격(Linux) 세션에서는 사용자 MEMORY(`C:/Users/rmsep/...`) 접근 불가 → WORKFLOW [10] 사용자 MEMORY 갱신은 건너뛰고 그 사실을 보고에 명시. 커밋 해시는 git 미사용 방침이라 사용자가 프롬프트로 제공한 값을 그대로 기재(직접 조회하지 않음).
- 문서 인덱스 동기화 감사(2026-07-16): `_Tasks`/`_Logs` 제외 상시 Docs Markdown 목록을 실제 파일 목록 기준으로 확인하고, AGENTS.md / document-manager.md / GameSystemRules.md의 누락을 보정. 누락되기 쉬운 문서: `GameSystemRules_CanvasSortingOrder.md`, `GameSystemRules_Sound.md`, `Assets/VFXSFXGuide.md`, `Assets/VFXSFXList.md`, `Skills/SKILLS_GUIDE.md`, `AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.
- AAB 최적화 문서 관계(2026-07-16 정리): 최종 수치/적용 변경/롤백 기준은 `AABSizeOptimization.md`가 권위 문서. `BuildAssetOptimizationReport.md`는 빌드 에셋 import 감사/중간 리포트, `UnusedAssetAudit.md`는 미사용 에셋 탐지와 삭제 판단 근거 기록으로 둔다.
### 2026-07-16 - Profile/ranking cloud task documentation

- Updated project status, roadmap, work history, and task testcase for `Assets/_Project/Docs/_Tasks/2026-07-16/12_10_main-profile-cloudsave-leaderboard-port/`.
- Documented that profile/ranking cloud integration is complete and email verification abandonment is the next follow-up task.

- 신규 기능(전략 핸들러 아키텍처) 문서화 실사례 — 도끼병 휩쓸기형 AoE(2026-07-17, task `_Tasks/2026-07-16/18_06_battleaxe-aoe`): task Plan 하단 "설계 변경 이력 1~4"가 최종 구현 명세라 Research/Plan 본문은 무수정, Plan 최하단에 "완료 결과(실기 PASS)"만 append(Testcase.md는 TC/QA 미지시라 미생성). 갱신처: StatsReference(헤더 날짜 + BattleAxe 행 attackRange 0.5→0.75·비고 재작성 + 범위 공격 규칙 휩쓸기형에 "구현 확정" 불릿), GameSystemRules_Units.md **규칙 23~27 신설**(마지막 규칙 22 뒤 "특수 공격 시스템 규칙" 새 섹션 — 23 전략 핸들러/24 월드 부채꼴 판정/25 SpecialAttackConfig 튜닝/26 AoE 연출 동시 방출/27 클립 OnAttackHit 주입), GameSystemRules.md 인덱스 유닛 빠른참조에 "특수 공격 시스템" 불릿 추가(파일 목록 표 1줄 설명·AGENTS.md 파일 설명은 신규 파일 아니라 무수정 — 전투 연출 동기화도 미기재였던 선례 따름), TDD("특수 공격 전략 핸들러 구조" 서브섹션을 애니메이션 상태 동기화 서브섹션 뒤 + 변경 이력 0.22.0), PROJECT_STATUS(헤더 현재단계 + 신규 `####` 섹션 6행), ROADMAP(헤더 + 우선순위 표 ✅완료 행 + 특수 타격 5종→4종 갱신 + D-4 진행 노트 + F-4 BattleAxe 완료 처리), WORK_HISTORY(마일스톤 prepend), game-programmer/project-orchestrator/game-design-lead MEMORY prepend. **코드+밸런스 변경**이라 3개 MEMORY 모두 갱신(qa-tester는 이번 버그 수정/취약점 발견 없어 무수정).
- 규칙 번호 이어붙이기 재확인: GameSystemRules_Units.md는 2026-07-13 시점 규칙 22까지 → 이번에 23~27 부여. 신설 규칙은 항상 파일 마지막 규칙 번호를 Read로 확인 후 이어서 부여(규칙 간 상호 참조 명시: 24가 규칙 16·6 참조, 26이 규칙 18·19 참조, 27이 규칙 17 참조).
- SO 튜닝값 함정 교훈(에셋 생성 ≠ 씬 배선)은 규칙 문안(규칙 25)·game-programmer/project-orchestrator/game-design-lead MEMORY에 분산 기록. "유닛 attackRange vs 특수 sweepReach 별개" 혼동 주의도 규칙·밸런스 메모 양쪽.
- 간헐(intermittent) 버그 수정의 "초기 정상·지속 관찰 중" 상태 문서화 패턴(2026-07-17 매치메이킹 404, task `_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/`): 사용자가 "초기 실기 정상 확인, 단 간헐 버그라 지속 테스트 중"으로 상태를 못박은 경우 **✅ 완료로 소거 금지**. 상태 마커는 **🔵 "초기 정상·지속 관찰 중"**, 비활성화(주석)한 레거시 코드는 **최종 삭제하지 않았음**을 명시(지속 테스트 확정 후 별도 삭제). 갱신처: PROJECT_STATUS(헤더 현재 단계 + 네트워크 버그 섹션 근처 신규 dated 표 1행 🔵), ROADMAP(헤더 + 우선순위 표 🔵 행 + Phase A에 A-2 항목 🔵), WORK_HISTORY(마일스톤 prepend), game-programmer MEMORY(최근 작업 prepend 🔵), 공용 `.claude/MEMORY.md`(공통 교훈 1줄, 관찰 중 상태·task 경로 포함). 매칭/네트워크 전용 GameSystemRules 파일은 없으므로 새로 만들지 않음(불일치 없으면 무수정). Testcase는 사용자 미요청이면 생성하지 말고 계획 대비 확정/변경은 Plan.md 하단 "실제 구현 결과" 절에 append(Research 원상태 유지, SDK 시그니처 확정·클라 참가 경로 일원화·RefreshCurrentLobbyAsync 신규 등). 커밋 해시는 사용자 제공값 그대로 기재. 로컬 사용자 MEMORY(Windows 경로)는 원격 Linux 세션에서 접근 불가 → 보고에 명시.
- 싱글플레이 AI 시스템 실기 "조건부 완료" 반영(2026-07-16): 사용자가 핵심 흐름(유닛 생산/건물 업그레이드)만 적당히 실기 확인(PASS, 문제 미발견)하고 반응 시스템(R1~R3)·3종족 시나리오 무작위 동작 정밀 검증은 미완인 경우의 기록 패턴. **완료(✅)로 소거하지 말고 조건부 완료(🔵) + "후속 정밀 검증" 잔여를 명시**. 갱신처: PROJECT_STATUS(싱글플레이 AI 시스템 표의 "Inspector 작업"⏳→✅ / "실기 테스트"⏳→🔵 조건부 완료 + 헤더 현재 단계에 한 줄), ROADMAP(우선순위 표 2행 조건부 완료 전환 + Phase C-0 "남은 작업"을 세부 정밀 검증 항목으로 재기술). 실기 진행 사실이 곧 전제 Inspector 작업 완료를 함의 → 대기였던 Inspector 항목도 함께 완료 처리. 이번엔 코드/버그 변경이 없어 game-programmer/qa MEMORY·GameSystemRules·TDD·GDD는 무수정(지시 범위=문서 3종만).

### 2026-07-16 - Email verification flow docs

- Added task docs at `Assets/_Project/Docs/_Tasks/2026-07-16/14_20_email-verification-flow-cleanup/`
- Updated `AuthSystemRules.md`, `PROJECT_STATUS.md`, `ROADMAP.md`, and `WORK_HISTORY.md` with email verification cancellation policy.

### 2026-07-18 - Email verification flow completion docs

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
