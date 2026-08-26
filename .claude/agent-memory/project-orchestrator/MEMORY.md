# Project Orchestrator Memory — Hexiege

## 2026-08-24 B3 blocked-start 최소 교정 상태
- 동적 건물로 대체 경로가 있어도 Unit48/59/69/96이 멈춘 직접 원인은 non-walkable authoritative start가 FlowField에서 빠져 `RequestMove`가 null이 된 계약 공백이다.
- 최소 slice는 `UnitMovementUseCase`의 authoritative-only 인접 egress와 Editor self-validation이며 UnitView/HexFlowField/NetworkTransform/prefab/package는 보존했다.
- 독립 QA 최초 P1(비권위 staged start까지 예외 확산)을 API 권한 분리로 닫고 Q/R 회귀까지 보강했다. 다음 gate는 Unity `[UAS-DIAG]` PASS→Android 재빌드→집중 실기/역할교대 MOVE·ROOT·CrossAudit. Unit30/회전은 별도 OPEN.

## 프로젝트 현재 상태 (2026-08-24)

### B3 회전 계측 PASS / 역할교대 MOVE terminal FAIL
- Unity self-validation 2종과 `root-rotation-replication-v1` 완전성 PASS. 역할교대 세션은 Android Host 54/54, Editor Client 53/53 endpoint evidence, drop/preflight 0이다.
- Unit 30 Red Pistoleer adapter failure 6·repeated recoverable repath 1로 Host MOVE terminal FAIL하여 공식 CrossAudit 중단. read-only 동일 revision 회전 잔차 7(`0.11~0.15°`)은 원인 분리 근거일 뿐 공식 pose 판정이 아니다.
- 다음 순서: Unit 30 반복 repath 최소 재현·교정 → 새 역할교대 공식 CrossAudit → NetworkTransform final convergence 교정. 25종/Legacy rollback 보류, B3 FAIL/OPEN.

### B3 v9 최종 통합 정적 코드 게이트 / 메뉴·실기 대기
- v8 Android recoverable preflight 반복을 typed 결과와 `CandidateUnsafe/RouteInvalidated → WaitingRepath`로 교정했다. corridor probe/final stage는 같은 seam을 쓰고 fatal만 `Rejected`다.
- commit accounting은 actual attempt 직전 planned, success 뒤 committed, END equality다. recoverable history는 positive successful commit에서만 clear하고 lifecycle retire는 server/client baseline·recoverable 이력을 제거한다.
- `origin/main` 60개 commit과 B3 의미 병합 완료, unmerged 0·staged 최신 해결본 일치. main `GameLog`·`LogSessionOwner`·combat shutdown·`IsSpawned`·research 수정과 B3를 모두 보존했다.
- observer `b3-movement-authority-v9`, post-merge Runtime/Editor Roslyn PASS, 독립 QA P0~P3 0. 증거 감사는 최신 device anchor + Editor daily, role/shared key/production schema exact/전체 BEGIN·END identity/EVIDENCE·FAIL/source gate를 요구한다. terminal 32줄·최대 968 UTF-8 byte, adapter 64/65와 release `Conditional` 경계다.
- 후속 Android v9 실제 경기는 확보했지만 RootCrossAudit가 false-INCONCLUSIVE여서 공식 판정은 OPEN이다. pre-merge UAS PASS는 재사용하지 않으며 사용자 RootCrossAudit 재실행 전 B3 FAIL/OPEN·역할교대/25종 회귀 금지다. Android recoverable repath는 0이 아니라 resolved/nonrepeat/no-cleanup 수렴을 요구한다.

### RootCrossAudit false-INCONCLUSIVE 교정 / 공식 재감사 OPEN
- `a0e690...8d8` 실제 Editor Host·Android Client의 MOVE full EVIDENCE·ROOT PASS와 수동 49/49 match를 확보했다. ROOT bucket 14/17 peer 비교는 공유 시간축이 없어 false-INCONCLUSIVE였다.
- 진단기만 validated MOVE `startedAt <=2.000초`로 교정하고 run 내부 bucket·stable overlap·fail-closed를 유지했다. Editor Roslyn PASS. 다음은 사용자 RootCrossAudit SelfValidate → 동일 경기 Analyze이며 전까지 B3 FAIL/OPEN이다.

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

## 2026-03-14 완료 작업 (비-네트워크)
- **팀별 피아식별 + Assault/Sniper 코드 연동 완료**:
  - `UnitType.cs`: Pistoleer=0, Assault=1, Sniper=2
  - `UnitFactory.cs`: `UnitTeamPrefabSet` struct, `_bluePrefabs`/`_redPrefabs` 팀+타입별 분기
  - `BuildingFactory.cs`: `BuildingTeamPrefabSet` struct, `_bluePrefabs`/`_redPrefabs` 팀별 분기
  - `ProductionPanelUI.cs`: Assault/Sniper 버튼+초상화+생산 로직
  - `UnitStats.cs`: Pistoleer HP=30/ATK=6, Assault HP=50/ATK=1, Sniper HP=30/ATK=10
  - `UnitProductionStats.cs`: Pistoleer 5초/50골드, Assault 10초/100골드, Sniper 15초/200골드
- **팀별 초상화 동적 업데이트**: ProductionPanelUI/BuildingPlacementUI — Show() 시 팀별 스프라이트 교체
- **전투 범위 epsilon 제거**: `UnitCombatUseCase` +0.1f 제거 → `AttackRange * HexMetrics.TileHeight` (타일 점령 버그 수정)
- **공격 애니메이션-타격 동기화 (Animation Event 방식)**:
  - `AnimationEventRelay.cs` 신규 생성 (Animator 자식에 부착, OnAttackHit 릴레이)
  - `UnitView.cs`: OnAttackHit() + HitReactionCoroutine() 추가 (scale punch, 순수 비주얼)
  - Attack.anim 3개에 Animation Event 추가 (타격 프레임에 OnAttackHit)
  - 프리팹 6개에 AnimationEventRelay 부착, Root Motion OFF 확인
- **유닛 스탯 재조정** (ATK 값 변경):
  - Pistoleer ATK 3→6 (DPS=3, cooldown≈2.0s)
  - Assault ATK 6→1 (DPS=5, cooldown≈0.2s)
  - Sniper ATK 20→10 (DPS≈3.3, cooldown≈3.0s)
- **유닛 메시 방향 보정**:
  - Assault/Sniper 하위 Mesh 오브젝트 Y 회전 30° 설정 (이동 방향 보정)
  - _meshYOffset: 공격 방향 보정 전용 (CalculateAttackAngle만 영향, 추후 테스트 조정 예정)

## 3D 전환 시 수정된 파일 (참고)
- Phase 1: `HexMetrics.cs`, `ViewConverter.cs`, `CameraController.cs`, `GameBootstrapper.cs`, `InputHandler.cs`
- Phase 2: `BuildingFactory.cs`, `UnitFactory.cs`, `UnitView.cs` (삭제: `FrameAnimator.cs`, `UnitAnimationData.cs`, `PistoleerAnimData.asset`)
- Phase 3: `CameraController.cs` (tilt), `GameBootstrapper.cs` (Z오프셋)
- Phase 4: Meshy.ai 에셋 통합 예정
### 2026-07-16 - Lobby profile/ranking cloud feature status

- Profile/Ranking cloud integration is ready to merge to main as a completed vertical slice: Firebase verified login -> nickname setup -> lobby profile/ranking display.
- Scope includes Cloud Save profile, Leaderboards ranking, nickname setup/change UI, editor setup scripts, scene wiring, package additions, and default UI layout pass.
- Known deferred item: email verification flow robustness. The verification screen currently relies on current Firebase user email and lacks explicit handling for sign-up users who abandon verification. Next task should add explicit email display state, distinguish fresh sign-up vs existing unverified login, and define account deletion/sign-out behavior.
### 2026-07-16 - Email verification flow cleanup

- Current branch: `codex/email-verification-flow-cleanup`.
- Scope: email verification display/context/back behavior, not UI layout polish and not server-side stale unverified account cleanup.
- Manual verification needed in Unity Editor: compile, signup email display, signup cancel deletes unverified Firebase user, existing unverified login back signs out without deletion.

### 2026-07-18 - Email verification flow completed

- User device PASS: email display, signup cancel popup, Firebase unverified user deletion, continue verification, verification relaunch, nickname setup relaunch.
- Completion docs updated in task Plan/Testcase, `AuthSystemRules.md`, `PROJECT_STATUS.md`, `ROADMAP.md`, and `WORK_HISTORY.md`.
- Remaining separate concern: long-term stale `emailVerified=false` account cleanup policy is not implemented in this slice.

### 2026-07-20 - 유닛 이동·공격 동기화 문서 기준선

- 브랜치 `codex/unit-movement-attack-sync-audit`에서 규칙 문서만 일괄 개정했다. 코드·프리팹·애니메이션 에셋은 미수정이다.
- 구현 순서는 계측 기준선 → Simulation/Visual Root 분리 → 서버 UnitActionSnapshot/AttackImpactResult → Legacy FIFO와 shadow 비교 → 25종 AttackProfile 이전 → 멀티 검증 → Legacy 제거다.
- 전환 중 경기 단위 `CombatSchemaRevision + AttackProfileHash + CombatPipelineMode`를 고정하고 single-writer/single-emitter를 지킨다. 같은 경기에서 유닛별 Legacy/v2 권위를 혼합하거나 진행 중 rollback하지 않는다.
- 권위 규칙은 `GameSystemRules_Units.md`와 `GameSystemRules_UnitCombatSynchronization.md`, 구현·에셋 차단 상태는 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`를 따른다.

### 2026-07-22 - Inferno/Quake 병합 후 전투 기준선

- main의 InfernoSpirit/QuakeSpirit 핸들러는 서버 피해 의미를 구현한 Legacy adapter다. v2 구현 완료 수는 여전히 0/25다.
- 우선순위는 Quake marker 주입/실측, Inferno marker 교정, 단일 피해 writer 복구, 권위 ActionSequence/ImpactResult 이전, Simulation/Visual Root 분리, 멀티 지연 QA 순으로 유지한다.
- 사용자 폰트 변경과 Quake 로그 `.meta`는 전투 문서 작업 범위 밖이므로 보존한다.

### 2026-07-22 - Unit ActionSequence Tracer A0 게이트

- Shadow Melee Sequence 구현과 사용자 멀티 Host 계측 완료. Editor self-validation PASS, scheduled/dispatch/unique 204/204/204, 누락·중복·타겟·facing 불일치 0, Windup 240ms 전건 일치다.
- dispatch 지연은 min 0.013ms / avg 9.105ms / p50 8.226ms / p95 19.862ms / max 29.386ms이고 33.333ms 이상은 0이다. Client 로그는 header only다.
- 기존 서버 피해·RPC·VFX를 유지한 Shadow 단계다. 당시 순수 sequencer는 후속이었으며 아래 A1에서 완료됐다.
- 이동/공격 방향과 시각 Impact 문제를 해결 완료로 승격하지 않는다.

### 2026-07-22 - Unit ActionSequence Tracer A1 게이트

- Pure Application `UnitActionContracts`와 stateful `UnitActionSequencer` 구현 완료. runtime-independent 상태·타임라인·사거리·결과 권한 계약과 revision/time fail-closed reducer를 제공한다.
- 사용자 Unity Editor 메뉴 PASS, C# 9/Application 및 Editor compile PASS, reflection `Validate*` 10 PASS, 최종 Standards/Spec P0~P3 지적 0건이다.
- 런타임 pose/result seam 및 피해·RPC·VFX는 미연결이므로 세 사용자 증상과 v2 전환은 미완료다.
- 다음 조율 게이트는 A2 server-authoritative pose seam shadow다. 기존 피해·RPC·VFX single-writer/single-emitter를 유지한다.

### 2026-07-27 - Unit ActionSequence Tracer A2 게이트

- pure pose 계약, `UnitView` read-only Legacy adapter와 서버/SpearMan one-cycle reducer Shadow를 구현·검증했다. 공격자 사망은 pending observer 삭제가 아니라 `MarkDead`/`DeadTerminal`, 용량 초과는 canonical `capacity-evicted` terminal skip으로 닫는다.
- 사용자 Editor self-validation PASS. Host 18:04:04 완료 회차 상관관계 누락·중복 0, attacker-dead 2건 terminal, eviction·예외 0. schedule 429 / dispatch 428의 차이 1건은 로그 종료 시 Impact 전 in-flight다. Client 18:09:48 observer 0.
- 기존 피해·HP·RPC·VFX·이동 writer와 서버 권위 Legacy 분기는 유지했다. A2 런타임 게이트 PASS 후 다음 조율 게이트는 Tracer B Simulation Root / Visual Root 분리다.

### 2026-07-27 - Unit ActionSequence Tracer B0 게이트

- 50개 프리팹(Human 16 / Spirit 18 / Transcendence 16)의 read-only 구조 감사와 rollback manifest 생성이 PASS했다. 예상 migration은 VisualRoot create 50 / reuse 0 / direct-child move 58이다.
- 사용자 dry-run 연속 2회가 각각 50/50, errors 0, assetsModified 0이며 aggregate manifest SHA-256 `1d1043ff2ea440a5f25d24a9e006bca8739ecb514b4e362f8dd6c01504ae1dcd`로 일치했다. prefab/scene Git diff와 mutation API 호출은 0이다.
- 전체 직렬화 상태가 아니라 NGO 의미 설정 allowlist를 결정적으로 해시하고, Unity Console 절단에 견디도록 프리팹별 로그와 종합 digest를 분리한다.
- B1은 50개 프리팹 원자적 migration과 rollback 검증이며, B2 Shadow/B3 경기 단위 writer 전환 전에 완료해야 한다. B0 PASS를 실제 Root 분리나 세 증상 해결로 확대하지 않는다.

### 2026-07-27 - Unit ActionSequence Tracer B1 조건부 게이트

- 50개 프리팹 asset migration, journal completed와 Apply 재실행 `[NO-OP]`는 PASS다. `VisualRootProjector`/`PresentationPoseProvider` seam과 클라이언트 Simulation Root write 제거도 구현됐다.
- 2026-07-27 당시 B1 전체 게이트는 열려 있었다. rollback failure injection과 Host/Client·Blue/Red runtime smoke가 미완료였으며, 후속 판정은 아래 2026-07-29 항목을 따른다.
- 신규·교체 프리팹 authoring 권위는 `NET-ROOT-004` + Asset Matrix 체크리스트 + fail-closed validator다. 50개용 bulk migration은 반복하지 않으며, 존재하지 않는 Legacy `SetupNewUnitPrefabs.cs`를 현행 도구로 안내하지 않는다.
- validator는 root Animator/Renderer 0, Animator별 동일 GO relay와 Root Motion off, 파일명과 Blue/Red pair·roster 기준을 강제한다. Collider는 B1 검증 범위가 아니다.

### 2026-07-29 - Unit ActionSequence Tracer B1 rollback 게이트 PASS

- graceful failure injection index 0/24/49는 모두 `JournalRecovered=true`, `VerifiedFileCount=100`, `InitialAnalyzerPassed=true`다.
- crash index 24 강제 종료 후 별도 프로세스 `RecoverCrash`가 PASS했다. 복구 전 prefab mismatch 25 / meta mismatch 0 / VisualRoot 25 / projector 25였고 복구 후 primary 50 prefab + 50 meta가 100/100 원상복구됐다.
- primary Unity compile Tundra success와 `[UAS-DIAG]` self-validation PASS를 함께 확인했다.
- B1 overall은 아직 OPEN이다. Android 1대와 Unity Editor counterpart의 역할교대 Host/Client `[UAS-ROOT-POSE]` runtime smoke와 Blue/Red 교차 감사 전에는 B2 서버 이동·방향 Shadow를 시작하지 않는다.

### 2026-07-29 - Unit ActionSequence B1 Collider 진단 가정 제거

- 2026-07-27 B1에서 근거 없이 추가된 Collider 존재·배치 조건은 B1 out-of-scope다. 공통 계약, runtime observer, Editor validator와 self-validation에서 완전히 제거하고 optional/root-only 계약도 남기지 않는다.
- B1 권위는 network component placement, identity VisualRoot, root Animator/Renderer 0, projector/ref, Root Motion off, Animator별 relay, 서버 single-writer와 client Simulation Root write 금지다.
- 최종 게이트는 Android 1대와 같은 코드 리비전의 Unity Editor counterpart를 역할교대하는 두 경기다. Match A는 Editor Host Blue file + Android Client Red Logcat, Match B는 Android Host Blue Logcat + Editor Client Red file을 짝지어 분석한다.
- 각 경기의 동일·available sharedSessionKey, role/isFlipped, 180초 coverage, END PASS/errors 0과 외부 stable cross-audit가 필요하다. Windows/Standalone build는 사용하지 않으며 Android 2대는 release E2E·성능·호환성 권장 항목이다. B1 overall OPEN과 B2 시작 금지를 유지한다.

### 2026-07-30 - Unit ActionSequence B1 게이트 종료

- Android 1대와 Unity Editor counterpart의 Host/Client 역할교대 두 경기에서 양쪽 로컬 PASS와 cross-pose mismatch 0을 확인했다. B1은 COMPLETE이며 B2 서버 이동·SimulationFacing Shadow가 다음 활성 게이트다.
- B1 완료 범위는 Root 분리, NetworkTransform 안정 수렴, 50개 프리팹 migration·멱등성·rollback이다.
- B3 writer 전환, 공격 방향, Impact/피해 타이밍, result seam, 25종 이전·멀티 QA와 Legacy 제거는 계속 P0 미완료다.

### 2026-08-03 - Unit ActionSequence B2 게이트 종료

- B2 서버 이동·SimulationFacing read-only Shadow는 6개 인정 멀티플레이 경기에서 25/25 UnitType의 Blue/Red 누적 표본을 확보해 COMPLETE다. accepted decision 545,190회, manifest entry 420개이며 인정 세션의 decision/scope/authority/log-integrity 오류 카운터는 모두 0이다.
- 첫 4종은 observer v4, 나머지 21종은 endpoint `NoIntent`가 보강된 v5다. reducer schema는 모두 `b2-movement-reducer-v1`이며 각 유닛을 Host/Client 양 역할에서 각각 검증한 것은 아니다.
- B2는 진단·분류 완료다. 실제 이동·바라보기 writer 교정은 B3 전까지 미완료이고, `legacyMovedWhileShadowAlign`은 B3가 필요한 직접 근거다.
- 다음 활성 게이트는 B3 경기 단위 25종 이동·SimulationFacing single-writer 전환이다. 공격 방향과 Impact/피해 타이밍은 Tracer C/D 이후까지 미완료다.

### 2026-08-18 - B3 최신 main 통합과 진행성 교정

- `origin/main`의 연구·상태효과·스킬·MistShrine·빙결/둔화를 B3 서버 권위 이동과 merge했다. merge commit은 `9dd9b627`, 안전 stash `277a3ea54e91cd778829943491e7f4827f9649db`는 유지한다.
- B3 유한 재탐색 guard 구현과 Unity compile/self-validation, 문서 정합성 검사는 PASS다. 전체 상태는 Android Host 회귀 전까지 FAIL / OPEN이다.
- 다음 순서: 새 Android Development Build → Android Host 정지 재현 회귀와 다른 유닛/경기 진행 확인 → Editor/Android 역할교대 → 25종 전체 → 다음 경기 Legacy rollback.
- 공격 타겟/공격 방향과 시각 Impact/실제 피해 시점은 이번 교정 범위 밖이며 후속 Tracer에서 별도로 진행한다.

### 2026-08-25 - B3 집중 교정 종료와 Tracer C 진입 게이트

- B3 v10 중심 이동·건물 안전 Chase·전방 중심 복귀는 focused multiplayer PASS다. 25종·반대 역할·Legacy rollback은 제거하지 않고 Tracer C 이후 권위 전환 전 통합 회귀로 이관한다.
- ROOT CrossAudit의 긴 Android terminal 절단은 compact END(최악값 803 UTF-8 byte)와 production preflight로 교정했다. Runtime/Editor Roslyn 및 Unity self-validation 2종 PASS다.
- 기존 절단 로그는 INCONCLUSIVE 이력으로 보존하고 재판정하지 않는다. 진단기만을 위한 별도 빌드/실기는 생략하며 다음 새 빌드부터 교정된 채점기를 사용한다.
- 다음 활성 단계는 Tracer C Phase 4 공격 회차 Shadow와 서버 권위 TargetId/AimDirection·AlignToAttack 5°/8°다. Shadow 동안 Legacy 피해·HP·RPC·VFX writer는 유지하고 이중 writer를 금지한다.
> 여기 있던 「2026-03-14 완료 작업 (비-네트워크)」 · 「3D 전환 시 수정된 파일 (참고)」 · 2026-07-16/07-18 인증 플로우 진행 기록은 2026-08-24에 [project-history.md](project-history.md) 로 옮겼다. 모두 지나간 시점의 기록이라 현재 상태와 섞이면 오해를 만든다.
