# Project Orchestrator Memory — Hexiege

> # 🔴 ARCHIVE — the `project-orchestrator` agent was retired on 2026-09-02
>
> **The agent definition `.claude/agents/project-orchestrator.md` was deleted.** There is no
> `project-orchestrator` agent to call any more.
>
> **Why it was retired.** This agent's whole job was to coordinate *other* agents, but
> **a subagent cannot call another agent** — that is a harness-level restriction and no
> setting changes it. So an agent invoked to coordinate had nobody to coordinate and did
> all the work itself, which is not coordination. On 2026-09-01 the agent reported this
> about its own run: it could not honour `CLAUDE.md` rule 3 because no tool for calling
> another agent was available to it.
>
> **Coordination is now the main session's job.** The main session is top-level, so it can
> call each specialist agent directly, and that is how this project has actually worked.
> The *criteria* for when coordination is needed (design + implementation together / three
> or more files / reviewing an agent's output) remain valid and live in `CLAUDE.md` rule 3
> and `WORKFLOW.md` [5].
>
> **This folder is kept on purpose — do not delete it.** `project-history.md` and
> `roadmap-3d.md` are **project history, not agent role material**; retiring the agent does
> not make that history wrong. Deleting it would violate `.claude/MEMORY.md` memory rule 6
> (delete only after confirming an entry is wrong) and rule 16 / `check_docs.py` check `[7]`
> (an agent folder's total line count must not decrease). This project already has one
> unrecovered **-378 line** loss of exactly this shape.
>
> **Read the rest of this file as a record of how the retired agent worked**, not as
> instructions for a live agent. The delegation patterns and required-context checklist
> below still describe how this project delegates — the main session is now the caller.

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음, 모든 서브에이전트 포함)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 attack direction 작업 전체 삭제 (복구 불가)
- 서브에이전트(game-programmer 등)에 작업 위임 시에도 이 규칙을 반드시 명시할 것
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## 토픽 파일 인덱스

| 토픽 파일 | 내용 |
|---|---|
| [project-history.md](project-history.md) | 2026-03-14 ~ 2026-07-31 과거 프로젝트 스냅샷 아카이브. **현재 상태가 아니다** — 현재 상태는 바로 아래 절 |
| [roadmap-3d.md](roadmap-3d.md) | 「3D 전환 + 네트워크 점검 로드맵 (2026-02-27 확정)」 — Phase 0(네트워크 점검) ~ Phase 4 실행 계획. 3D 전환은 2026-03-01에 완료됐으므로 **당시 계획서로만 참조** |

## 프로젝트 현재 상태 — 기준일 2026-08-08 (이 절만이 현재 상태다)

> 종전에는 「프로젝트 현재 상태」라는 **똑같은 제목의 절이 7개**(2026-08-08 / 07-31 / 06-23 / 04-13 / 04-06 / 03-26 / 03-19) 있어 에이전트가 **과거 상태를 현재로 오해**할 수 있었다. 2026-08-24에 이 절만 남기고 나머지 6개는 [project-history.md](project-history.md) 로 옮겼다(삭제 아님 — 원문 그대로 보존).

### 2026-08-08 완료 — 랠리포인트 조준 시 BlockingOverlay 잔존 버그 수정 (실기 PASS · 커밋 `9a19cd5`)
- 배럭 팝업에서 랠리포인트 버튼을 누르면 팝업만 숨겨지고 공유 반투명 오버레이가 남아, 맵 탭이 오버레이에 먹혀 `Close()`가 실행되며 집결지 지정이 취소된 것처럼 보이던 버그.
- **작업 흐름**: 코드 조사(game-programmer 정적 분석, 랠리포인트 구조·취약점 6건 도출) → `Research.md` → `Plan.md`(사용자 승인) → game-programmer 구현 → 사용자 실기 테스트 **PASS**. TC 문서는 사용자 미지시로 미작성(WORKFLOW [5-1] 준수).
- **결과**: 코드 변경 `Presentation/UI/ProductionPanelUI.cs` **1파일 2줄**(순수 추가, 기존 로직 제거 없음). `OnRallyPointClick()` 오버레이 해제 + `CompleteRallyPointSetting()` 참조 카운터 반납. 계획과 구현 100% 일치(추가 변경 없음).
- **조율 교훈**: 단일 파일·2줄이어도 "①만 고치면 ②가 새 버그를 만든다"는 결합이 있어 **두 지점을 한 Plan에 묶은 것이 핵심**이었음(기존 버그가 다른 누락을 상쇄하고 있던 구조). Research에서 발견한 나머지 랠리포인트 결함 5건은 범위 밖으로 분리해 별도 작업 후보로 기록.
- task `_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`. 규칙 근거 `GameSystemRules_UI.md` 공통 UI 규칙 5, 보조 `GameSystemRules_Buildings.md` 랠리포인트 시스템 규칙 2(규칙 문서 변경 불필요 — 코드가 규칙을 다시 준수하도록 맞춘 수정).

## 에셋 폴더 구조 확정
```
Assets/_Project/
├── Models/Units/Pistoleer/Pistoleer.fbx
├── Models/Buildings/Castle/, Barracks/, MiningPost/
├── Models/Tiles/HexTile/
├── Animations/Units/Pistoleer/Pistoleer_[State].anim
├── Textures/ → tex_[name]_albedo.png
├── Materials/ → mat_[name].mat
└── Prefabs/Units/, Buildings/, Tiles/, Misc/
```

## 3D 렌더링 핵심 사항
- 카메라: Orthographic + X축 55도 틸트 (Clash of Clans 스타일)
- 좌표계: XZ 평면 (Y=0 바닥, Y=높이)
- 유닛 Animator 파라미터: `IsDead`(bool) 1개만 사용 — IsWalking/Attack trigger 제거됨 (Animator.Play() 직접 호출 방식)
- 방향 표현: Y축 회전 (E=0°, NE=60°, NW=120°, W=180°, SW=240°, SE=300°)
- sortingOrder 완전 폐기 → Z-depth 기반 자연스러운 렌더링
- FrameAnimator.cs, UnitAnimationData.cs, PistoleerAnimData.asset 삭제됨

## 사용 가능한 에이전트

| 에이전트 | 역할 | MEMORY 경로 |
|---------|------|-------------|
| game-programmer | 코드 구현, 버그 수정, 아키텍처 적용 | `.claude/agent-memory/game-programmer/MEMORY.md` |
| game-design-lead | 게임플레이 설계, 밸런스, 기획 결정 | `.claude/agent-memory/game-design-lead/MEMORY.md` |
| qa-tester | 구현 검증, 버그 체크리스트, 패턴 분석 | `.claude/agent-memory/qa-tester/MEMORY.md` |
| asset-prompt-crafter | Meshy.ai 3D 모델 프롬프트 생성 | `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` |
| project-orchestrator | 작업 분해, 위임, 조율 (본인) | `.claude/agent-memory/project-orchestrator/MEMORY.md` |

## 작업 유형별 위임 패턴

| 작업 유형 | 담당 에이전트 | 비고 |
|----------|-------------|------|
| 새 기능 코드 구현 | game-programmer | 아키텍처 규칙 컨텍스트 전달 필수 |
| 버그 수정 | game-programmer | 관련 파일 경로 + 증상 전달 |
| 게임 설계 결정 | game-design-lead | 현재 구현 상태 컨텍스트 전달 |
| 구현 후 검증 | qa-tester | 변경된 파일 목록 + 예상 동작 전달 |
| 3D 모델/에셋 제작 | asset-prompt-crafter | Meshy.ai 프롬프트, FBX 파이프라인 |
| 복합 기능 (설계+구현+검증) | 순차: design-lead → programmer → qa-tester | |

## 위임 시 필수 컨텍스트 항목
모든 위임 시 반드시 포함:
1. 관련 파일 경로 (**리포지토리 루트 기준 상대경로**, 예: `Assets/_Project/Scripts/...`) — 탐색 비용 절감. ⚠️ 윈도우 절대경로(`d:/Dmain/...`)를 넘기지 말 것 — 작업 환경은 원격 리눅스 세션(`/home/user/Hexiege`)이라 그 경로는 존재하지 않으며, 2026-08-20 MEMORY.md 소실 사고 원인 ②(죽은 윈도우 경로로는 기존 파일을 못 찾음)와 같은 성질이다.
2. Clean Architecture 레이어 규칙 (Domain이 Core 참조 불가 등)
3. NGO API 제약 명시 (ServerRpc/ClientRpc 이름 규칙, NetworkBehaviour=Infrastructure만, RPC 파라미터 직렬화 타입)
4. 현재 프로젝트 상태 요약
5. 해당 에이전트 MEMORY.md 경로

## 작업 완료 후 메모리 업데이트 체크리스트
- [ ] game-programmer MEMORY.md: 새 파일/클래스/API 매핑 추가
- [ ] qa-tester MEMORY.md: 새 취약 지점, 테스트 체크리스트 항목 추가
- [ ] game-design-lead MEMORY.md: 구현 완료 항목 이동, 미결 항목 갱신
- [ ] 공용 `.claude/MEMORY.md`: 아키텍처 결정사항 반영
- [ ] project-orchestrator MEMORY.md: 현재 상태 요약 갱신

## 사용자 워크플로우 선호사항
- 사용자는 총괄(project-orchestrator)에게 요청 → 총괄이 각 에이전트에게 분배
- **규모/복잡도 관계없이 모든 작업은 시작 전 반드시 계획을 사용자에게 검토받을 것 — 승인 없이 바로 작업 절대 금지**
- **단순 작업(1개 파일, 명확한 로직 변경)에서 project-orchestrator 생략 여부는 반드시 사용자에게 먼저 물어볼 것 — 임의 판단 금지**
- 한국어로 소통
- 에이전트 완료 후 반드시 메모리 업데이트

## 프로젝트 핵심 제약
- 스크립트: `Assets/_Project/Scripts/` 아래 레이어별 폴더
- No Assembly Definitions — 네임스페이스 규약으로만 레이어 경계 관리
- Domain → Core 참조 금지 (HexOrientationContext 정적 홀더 패턴 사용)
- GameBootstrapper = 유일한 의존성 조합 루트
- ViewConverter: Red팀 좌표 반전, 스프라이트/메시 뒤집기 없음
  - 올바른 초기화 순서: ViewConverter.Setup() → LoadMap()
  - Setup() 전에 HexMetrics.Orientation과 TileWidth/TileHeight를 반드시 사전 설정
  - LoadMap() 내 Reset()은 싱글플레이 경로(isNetworkMode=false)에서만 실행
- 멀티플레이: NGO 2.9.2, Enable Scene Management = ON
- **3D 전환 후 sortingOrder 불필요 → Z-depth로 대체**

## 네트워크 알려진 미완성/점검 필요 항목
- BuildFailedClientRpc/EnqueueFailedClientRpc: UI 피드백 미구현 (RPC 구조 완성, 함수 내부에 UI 호출만 추가하면 됨) → UI 기획 후 구현 예정
- 재접속 기능: 실제 재접속 흐름 없음 (30초 대기 후 ForceWin만 구현)
- 멀티플레이 로비 UI: 기본 Host/Join만 구현 (방 목록/대기 화면 미완성)

## 네트워크 완료된 항목
- ~~InputHandler 유닛 이동~~ → 유닛 이동은 AI 전용, 플레이어 직접 이동 기능 자체 삭제됨
- ~~자동생산 멀티플레이 미지원~~ → 완료: ToggleAutoServerRpc + AutoProductionChangedClientRpc 구현됨
- ~~생산 큐 클라이언트 UI 동기화~~ → 완료 (2026-03-01): SyncQueueStateClientRpc로 즉시 갱신
- ~~Siege/AI 이동 서버·클라이언트 독립 실행 (화면 불일치)~~ → 완료 (2026-03-07): BroadcastServerMove + BroadcastMoveClientRpc로 서버 권위 동기화

> 여기 있던 「2026-03-14 완료 작업 (비-네트워크)」 · 「3D 전환 시 수정된 파일 (참고)」 · 2026-07-16/07-18 인증 플로우 진행 기록은 2026-08-24에 [project-history.md](project-history.md) 로 옮겼다. 모두 지나간 시점의 기록이라 현재 상태와 섞이면 오해를 만든다.
