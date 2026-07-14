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
- 환경 주의: `.claude/MEMORY.md`의 절대 경로는 Windows(`d:/Dmain/...`)로 적혀 있으나 실제 작업 환경은 Linux(`/home/user/Hexiege/`). 파일 접근은 항상 `/home/user/Hexiege/` 기준.
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
- "엣지 트리거 RPC → NetworkVariable 레벨 동기화" 전환 서사(비개발자 설명 재사용 가능): 상태처럼 "현재 값 자체가 의미"인 것은 1회성 신호(엣지)가 아니라 값 공유(레벨)로 다뤄야 스폰 레이스/신호 유실에 구조적으로 안전. 계측 잔여 수치(41건)가 정상 동작(우회)을 오탐할 수 있어 무조건 버그로 보지 않고 성격 확인 후 코드 무수정 종결 — 이 "오탐 종결"을 문서에 지표 한계로 명시하는 패턴.
- Plan 대비 변경 기록 시 자주 나오는 형태: 비활성화 예정이던 코드가 실제로는 다른 기능 가드였음(`_combatAnimationSent`=데미지/타겟 RPC 게이팅) → 제거하지 않고 유지, 그 사유를 규칙 문안·완료 섹션 양쪽에 명시.
- 로그인/인증 문서 위치(2026-07-14 닉네임 흐름 작업 기준): 규칙 문서는 `AuthSystemRules.md`(닉네임 수집 시점=규칙3, 회원가입 후 처리=규칙4, UGS 연결/OIDC=UGS 연결 섹션) + `GameSystemRules/GameSystemRules_UI.md`("닉네임 설정 화면" 규칙 4=완료 후 흐름). 구현 세션에서 이 두 문서를 이미 갱신해 둔 경우가 있으니 [12]에서 "재확인만" 지시면 diff 없이 일치 확인으로 종결. PROJECT_STATUS는 로그인 시스템 표가 두 곳(Google 로그인 실기 디버깅 섹션 ~L164 + 로그인 시스템 C# 구현 섹션 ~L418)에 나뉘어 있어 OIDC/닉네임 상태 갱신 시 양쪽 관련 행을 모두 확인.
- "미해결 잔여 이슈가 후속 작업에서 해결됨"을 기록할 때: 공용 `.claude/MEMORY.md`의 기존 교훈 문장은 삭제하지 말고 끝에 "→ YYYY-MM-DD 해결됨(아래 참조)"를 덧붙이고, 새 날짜 교훈 항목을 별도로 추가해 해결값(예: OIDC 등록값)을 정확히 기록. 히스토리 추적성 유지.
- 과대 기록 금지 실사례(2026-07-14): 로그인 3경로+닉네임 흐름은 완료지만 프로필 전적/랭킹(recordMatchResult 서버 연결)·닉네임 변경 UI·닉네임 패널 스프라이트는 "진행 중/미완"으로 별도 행 유지. 작업 지시가 "완료 vs 진행중" 구분을 명시하면 status 문서에 진행 중 행을 반드시 남길 것.
