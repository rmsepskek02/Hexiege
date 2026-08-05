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

### 2026-07-23 - 밸런스 확정 설계 반영(연구소 유닛 강화 + 전투 ×10) — "설계 확정 / 구현 예정"

- 코드 미착수 상태의 밸런스 확정 설계를 문서에 일괄 반영한 사례(구현/테스트 전이므로 **WORK_HISTORY.md는 추가 금지** — 완료 이력용). 갱신처: StatsReference(전면 재작성 — 최상단 배너 + 전 전투 수치 ×10 + 신규 강화 스탯 섹션), GDD(연구소 섹션 교체 + 유닛 스탯 표 ×10 + 헤더 버전/날짜), **신규 `GameSystemRules/GameSystemRules_Upgrade.md`**(RandomMap식 "상태: 확정 설계/미구현" 규칙 문서, 규칙 1~11), GameSystemRules_Units.md 규칙 44(방어력 감쇄, 규칙 43 뒤 신규 섹션), GameSystemRules.md 인덱스 + AGENTS.md 인덱스에 신규 파일 등록, task Research/Plan(초기안→확정값 in-place 갱신 + 최상단 "확정값 갱신" 배너), ROADMAP/PROJECT_STATUS(🔷 설계 확정/구현 예정 항목·섹션 추가, 헤더 날짜+설계확정 라인).
- **SSOT 인용 원칙**: 수치는 task 폴더 `BalanceReview.md`(old/new 대조표)에서 그대로 인용, 추정 금지. 개별 ×10 값의 권위 소스는 StatsReference로 일원화하고 다른 문서는 참조로 연결.
- **"구현 완료" 태그 + ×10 설계값 공존 처리**: StatsReference 비고의 특수공격 "구현 완료/확정"은 메커니즘 구현을 뜻하고 숫자는 ×10 설계 목표값임을 **최상단 배너에서 전역 명시** → 개별 태그 재작성 없이 정합. 비고 내 명시 수치(직접/DoT/힐/스플래시)는 ×10로 갱신하되 "로그 검증"은 "메커니즘 검증"으로 문구 조정.
- **task 계획문서는 예외적으로 in-place 갱신**: 보통 Research/Plan 본문은 히스토리 보존(하단 append)이지만, 이번은 "구현 계획 문서라 최종 확정 수치 기준으로 정리" 지시 → 본문 stale 값(K=20→120, 830→1000, 자연회복 0.5~2.5→3~15 등)을 직접 교체하고 최상단에 "확정값 갱신" 배너로 변경 이력만 남김.
- 신규 GameSystemRules 파일 등록 3곳: `GameSystemRules.md`(파일 목록 표 + 시스템별 빠른참조 블록), `AGENTS.md`(기획/설계 문서 인덱스 표). GDD 헤더는 버전 bump(1.9.0→1.10.0)+날짜+변경 노트.
- GDD 유닛 스탯 표에 **기존 stale 값**(Assault HP 50 vs StatsReference 40, Sniper atk 10 vs 18) 존재 → ×10 반영 시 StatsReference 권위값의 ×10(400·180)으로 정합화(GDD가 "StatsReference 권위" 명시했으므로 정당). 생산시간 등 불변 stale 항목은 표에서 제거하고 StatsReference 참조로 위임.

### 2026-07-31 - 연구소 유닛 강화 시스템 구현·멀티 실기 완료 반영 ("설계 확정/구현 예정" → "구현 완료")
- **선행 세션이 "설계 확정/구현 예정"으로 반영한 시스템(2026-07-23)이 구현·멀티 실기 완료된 경우의 일괄 상태 전환 사례.** 이번엔 구현·테스트 완료이므로 **WORK_HISTORY.md에 마일스톤 추가**(2026-07-23 설계 확정 때는 완료 이력이 아니라 추가 금지였던 것과 대비 — 완료 여부가 WORK_HISTORY 추가 기준).
- **갱신처(전량)**: `GameSystemRules_Upgrade.md`(상태 배너 구현 완료+×10 config 커밋 반영 고지, "구현 상태" 섹션 신설 = 완료/후속 보류 구분, **규칙 13 신설 = 연구 패널 UI 최종 설계**, 참고문서 링크 갱신) · `GameSystemRules_Units.md` 규칙 44(구현 완료+`DamageCalculator` 명시) · `GameSystemRules.md` 인덱스·빠른참조 · `AGENTS.md` 인덱스 · `StatsReference.md`(배너 "구현 완료 — ×10 config `.asset` 커밋 반영" + Tank/CannonCart 2배·×10·강화 섹션 태그 정정) · `GameDesignDocument.md`(버전 1.11.0·헤더 노트·연구소 섹션 구현 완료+UI 문단) · `PROJECT_STATUS.md`(헤더·"구현 예정"에서 완료 섹션으로 이동·미구현 표 연구소 행 완료) · `ROADMAP.md`(헤더·우선순위 행·C-2 완료) · `WORK_HISTORY.md`(2026-07-31 마일스톤 prepend) · task `Plan.md`(하단 "완료 결과" append — 히스토리 보존)·`BalanceReview.md`/`Research.md`(상단 상태 배너만 갱신, 수치 SSOT 유지) · MEMORY 5종(game-programmer 기존 Phase 1 항목 in-place 갱신+Phase 2 완료 prepend / qa-tester 스폰 레이스·MP PASS / game-design-lead 밸런스+구현 / project-orchestrator 완료 / 공용 `.claude/MEMORY.md`).
- **핵심 정정 = UI 설계 확정 변경**: 계획 "생산 패널 패턴" → 실제 "`ResearchPanelUI : BuildingPanelBase` + 매트릭스/진행 2-레이어(연구소 단위)". Plan "완료 결과"·규칙 13·GDD·PROJECT_STATUS·ROADMAP에 일관 반영.
- **과대 표기 금지 원칙**: 사용자가 "구현 완료 vs 후속 보류"를 명시 구분 지시 → 모든 문서에 후속 보류 5종(UI 레이아웃·매트릭스 헤더 아이콘·AI 연구 실기·MistShrine 힐 미구현·싱글 자연회복 실기)을 완료 표기와 나란히 명시. AI/MistShrine/싱글회복/UI레이아웃을 "완료"로 소거하지 말 것.
- **×10 커밋 반영**: config `.asset`(UnitStatsConfig·BuildingStatsConfig·SpecialAttackConfig)에 ×10 값이 커밋되어 저장소 기본값이 ×10(코드 폴백도 ×10, Inspector 값 우선). ×10 적용에 쓰였던 셋업 에디터 스크립트는 역할 종료 후 제거됨(더 이상 실행 불필요).
- Testcase.md는 사용자 지시로 미작성. 사용자 MEMORY(Windows 경로)는 Linux 세션에서 접근 불가 → 보고에 명시.

### 2026-08-04 - 스킬 건물 시스템 Phase 1 조준 좌표화·렌더링·버그수정 실기 PASS 반영
- **선행 세션이 "코드 완료·컴파일 미검증"으로만 남겨둔 대형 시스템(스킬 건물 Phase 1)이 이번 사이클(조준 좌표화·지면 데칼·취소버그·토스트)로 컴파일·씬 배선·실기 PASS된 경우.** PROJECT_STATUS.md에는 스킬 관련 내용이 **전무했음**(grep 0건 — 07-28 설계·07-31 구현이 미반영) → 이번에 Phase 1 완료 섹션을 **처음으로** 추가(완료된 시스템 최상단 신규 `####` 12행 표 + 후속/미완 blockquote 3종). WORK_HISTORY 마일스톤 prepend(완료라 추가 기준 충족).
- **과대 표기 금지가 이번 핵심**: 모든 문서에 완료(타입 A·B + 조준/UI/좌표화/렌더링/버그수정)와 미완(타입 C=enum만 선언·실행기 미구현 Phase 2 / 건물 파괴 시 UI 원복 / 구체 스킬 목록·수치 기획)을 나란히 명시. 타입 C·건물파괴 UI·기획을 완료로 소거하지 말 것.
- **핵심 서사(비개발자 설명 재사용)**: ① 좌표화 = "반경 판정은 원래 연속 원이라 무변경, 중심 입력만 타일 스냅→연속화" ② 조준원 데칼 = coplanar z-fighting을 ZTest LEqual+Offset로(ZTest Always 금지 — 유닛/건물까지 덮어 규칙 22-1 위반) ③ 취소버그 = 손 뗀 프레임 합성 마우스 좌표(0,0)가 캐시 폴백 가로챔 → release는 캐시 좌표만 ④ 토스트 = 기존 ToastUI 에셋 방식 재사용.
- **GameSystemRules_Skills.md 처리**: 규칙 본문(17·19·22·22-1·24·26)은 이미 확정안이 구현과 일치 → **무수정**. 다만 "구현 상태" 최상단 블록 + 규칙 17 위 "구현 상태 주의(설계 정정)" 주석은 "미구현/코드 미반영"이라 **stale** → "Phase 1 구현 완료·실기 PASS / 타입 C Phase 2 미구현"으로 갱신. 규칙 번호제 파일에서 규칙 본문이 아닌 **구현 상태 주석/블록**이 실기 완료로 stale해질 수 있으니 함께 검토(불일치 시 수정 = 이번 task 지시 범위).
- **task 문서**: 지시로 Research/Plan **둘 다** 하단에 "완료 결과"/"구현 후 사실 보강" append(Testcase.md 미생성). Plan은 확정 항목 / **계획과 달라진 점**(조준원 렌더링이 유력안 ZTest Always가 아니라 ZTest LEqual+Offset 데칼로 확정, 취소버그·토스트가 계획에 없던 후속) 구분 기록. Research는 §D 추정(depth 충돌)의 실제 원인(coplanar z-fighting) 보강.
- **에이전트 MEMORY(이번 갱신 범위 = 지시로 document-manager + game-programmer 2종만)**: game-programmer는 skill-aim 항목 in-place 갱신(헤더 실기 PASS + 취소버그·토스트 불릿 추가) / Phase 1 항목 헤더 실기 PASS 전환 / 토픽 파일 skill-aim-coordinate.md에 취소버그·토스트 절 추가. project-orchestrator는 논리상 대상이나 이번 지시 범위 밖이라 미수정(코드 변경 시 원래 갱신 대상 — 다음에 통합 반영 권장). qa-tester는 별도 QA/버그 발견 없어(실기 사용자 확인) 무수정. 커밋 해시는 사용자 제공값 그대로. 사용자 MEMORY(Windows) 접근 불가.

### 2026-08-05 - 스킬 타입 C(전역 상태변경) Phase 2 실기+멀티 PASS 반영
- **선행 세션이 "코드 구현 완료·컴파일/실기 미검증"으로 game-programmer MEMORY에 선반영해 둔 Phase 2(타입 C)가 실기+멀티(클라) PASS된 경우의 상태 전환 사례.** game-programmer MEMORY의 Phase 2 항목은 **in-place 갱신**(헤더 "미검증"→"실기+멀티 PASS", 9-5 빙결 bullet을 "둔화 라이브+빙결 Animator.speed=0 애니정지"로 갱신, UI 균일화·정리·실기결과·남은것 bullet 추가) — 새 항목 prepend 금지(중복 방지, MEMORY 규칙 41 재확인).
- **계획 대비 구체화 기록 = Plan §10 "Phase 2 완료 결과" append**: 9-5가 "이속배율 0 우선·A* 검증"에서 → ①이동코루틴 매 프레임 배율 재조회(둔화까지 라이브) ②빙결 Animator.speed=0+UnitAnimState.Frozen 클라동기화로 **두 갈래 구체화**. UI 버튼 균일화(CanvasGroup alpha=0 HideChildKeepLayout)·플레이스홀더 5슬롯 확장은 계획 외 후속으로 명시. Research는 §6로 "구현 후 사실 보강"만 append(§1~5 원상태 보존). Testcase.md 미생성(미지시).
- **GameSystemRules_Skills.md 처리**: 규칙 13 본문(타입 C 설계 = 버프/디버프/제어(둔화·빙결)/회복 한 시스템, 회복 전역 즉시)이 이미 구현과 일치 → **무수정**. 최상단 **"구현 상태" 블록만** "타입 C Phase 2 미구현"→"A/B/C 모두 구현·실기+멀티 PASS"로 갱신. 규칙 번호제 파일에서 규칙 본문이 아닌 상태 블록이 stale해질 수 있으니 함께 검토(task 지시 = 불일치 시만 수정).
- **cleanup/LogRules 교훈 분산 기록**: 진단 로그 제거로 `IRuntimeLogSink`/`RuntimeLoggerSink`가 **삭제**됨(grep 0건 확인) → game-programmer MEMORY의 CRITICAL 로깅 절이 이 어댑터를 "Application 로깅 표준"으로 기술하고 있어 향후 stale 주의(이번 지시 범위=Phase 2 완료 반영이라 CRITICAL 절 미수정, 다음 로깅 작업 시 정정 권장). 교훈 "로그 작업 전 LogRules 먼저 확인"은 공용 `.claude/MEMORY.md`+game-programmer MEMORY+WORK_HISTORY 3곳에 기록.
- **과대 표기 금지 재적용**: 완료(타입 C 3종 메커니즘)와 미완(건물 파괴 UI 원복·구체 스킬 기획·둔화 정렬 Lerp 잔여)을 모든 문서에 나란히. 플레이스홀더 5슬롯은 "테스트용"임을 명시(완료 스킬 목록으로 오인 금지).
- **stale "미구현" 문구 정정 패턴**: 헤더/현재단계/blockquote 등 여러 위치의 "타입 C Phase 2 미구현" 문구를 완료로 교체하되, 과거 dated 요약(2026-08-04 milestone 줄)의 "미완" 절은 삭제 대신 "이후 2026-08-05 구현 완료(위 참조)"로 보정해 히스토리 모순 회피.
- 갱신처(전량): 공용 `.claude/MEMORY.md`(공통교훈 prepend + Phase1 bullet 미완절 보정) · `PROJECT_STATUS.md`(헤더 날짜+2026-08-05 완료줄 신규·2026-08-04 미완절 보정·현재단계·스킬섹션 헤더/셀/타입C 3행 추가/blockquote 재작성) · `ROADMAP.md`(헤더+2026-08-05 완료줄·현재단계·우선순위표 타입C행 🔴→✅·D-1) · `WORK_HISTORY.md`(2026-08-05 마일스톤 prepend) · `GameSystemRules_Skills.md`(구현상태 블록) · task Plan §10·Research §6 append · game-programmer/document-manager MEMORY. 사용자 MEMORY(Windows)는 Linux 세션 접근 불가·git/커밋은 사용자 처리.

### 2026-07-18 - Email verification flow completion docs

- Completion update pattern: append task `Plan.md` completion result, add PASS results to task `Testcase.md`, update `PROJECT_STATUS.md`/`ROADMAP.md` progress sections from in-progress to complete, and replace the `WORK_HISTORY.md` in-progress entry with completed device verification results.
- Keep stale unverified account cleanup as a long-term policy item, not part of the completed client flow slice.
