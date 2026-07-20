# Project Orchestrator Memory — Hexiege

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음, 모든 서브에이전트 포함)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 attack direction 작업 전체 삭제 (복구 불가)
- 서브에이전트(game-programmer 등)에 작업 위임 시에도 이 규칙을 반드시 명시할 것
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## 프로젝트 현재 상태 (2026-07-17)

### 2026-07-17 완료 — 도끼병(BattleAxe) 휩쓸기형 AoE + 특수 공격 아키텍처 (사용자 실기 PASS)
- 특수 유닛 5종(BattleAxe/QuakeSpirit/TorrentSpirit/MushroomBomber/BloomFairy) 중 **첫 구현**. 이후 4종의 특수 공격 처리 기반 구조 확립.
- **전략 핸들러 아키텍처**: `ISpecialAttackBehavior`(계약) + `SpecialAttackContext` + `SpecialAttackRegistry`(UnitType 키) + `SweepAttackBehavior`. 모두 `Scripts/Application/Combat/`. 신규 유닛=핸들러+등록 1줄, `ExecuteAttack` 재수정 불필요.
- **피해 수렴점 단일화**: `UnitCombatUseCase.ExecuteAttack` 인라인 피해→`ApplyDamageToVictim` 헬퍼(주 타깃/AoE 공용, 멀티 HP 동기화 일관) + 특수 공격 훅 1줄.
- **휩쓸기 판정 = 월드 좌표 전방 부채꼴**: 초기 타일 기준→실기 후 변경. forward=공격자→주 타깃, XZ거리 ≤ `sweepReach`(실기 0.75) AND 각도 ≤ `sweepArcHalfAngle`(120°). `IEntityPositionProvider` 서버 권위.
- **튜닝 SO `SpecialAttackConfig`**(Infrastructure/Config), GameBootstrapper가 float 주입. 에디터 툴 `CreateSpecialAttackConfigAsset.cs` 멱등 자동화. ⚠️ 에셋 생성 ≠ 씬 배선(미배선 시 폴백 함정).
- **AoE 연출 동시 방출**: `HitPresentationQueue`가 `HitFrameTimes.Length≤1`이면 큐 전부 방출(휩쓸기 동시 표시), `>1`이면 1건.
- BattleAxe attackRange 0.5→0.75, Attack 클립 OnAttackHit 1.1667s 주입. main 최신화 병합 완료. 규칙 23~27, TDD 0.22.0. task `_Tasks/2026-07-16/18_06_battleaxe-aoe/`.
- 최신 전체 현황은 `Assets/_Project/Docs/PROJECT_STATUS.md` 참조.

### 2026-07-15 완료 — Android AAB 빌드 용량 최적화(main 반영)
- `codex/asset-size-optimization` 작업이 main에 병합됨. AAB 용량 **190.66 MB → 125.30 MB**(65.36 MB 절감).
- 핵심 변경: `Assets/_Project/Texture/Buildings/**`, `Assets/_Project/Texture/Units/**` Android max texture size `1024 → 512`.
- 함께 정리: `_Old` 미사용 에셋 디렉터리 7개, normal-map PNG 93개, roughness PNG 84개, 보수적 FBX import 조정. TMP Font Atlas 축소는 최종 AAB 효과가 작아 되돌림.
- 후속: 기기 QA에서 설치/실행, 로그인, 로비 UI 가독성, 인게임 유닛/건물 텍스처 품질, 팀 색상 변형, 공격 이펙트/emission 품질 확인.
- 상세 문서: `Assets/_Project/Docs/AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.

### 2026-07-13 완료 — 코드 정리 3건 (실기 통과, main 반영)
- **죽은 코드 제거**: `UnitView.StopMovement()` 삭제(호출 0건 Grep 전수). 런타임 불변. 커밋 `8840798`.
- **Animator 상태 의존 제거(리팩토링)**: 전투 종료 후 Walk 재개 3곳(`EnterCombatLoopV3` 멀티서버/싱글, `ResumeFromForwardTileV3`)의 `GetCurrentAnimatorStateInfo` 질의 → 로컬 추적 필드 `_currentAnimStateHash` + 헬퍼 `ResumeWalkAnimation`. CrossFade 4곳에서 필드 갱신. 서버/호스트/싱글 한정, 겉보기 불변. 실기 통과 후 주석 처리 블록 최종 삭제 완료. 커밋 `97adaad`+후속. task `_Tasks/2026-07-13/09_28_anim-resume-state-tracking/`.
- **Firebase 인증 게이트 제거**: `#if HEXIEGE_ENABLE_FIREBASE_AUTH`(main 528c7c6 도입)가 심볼 미정의 시 스텁 컴파일 → 로그인 무조건 실패. Firebase SDK는 로컬 임포트(`.gitignore`) 정책이라 게이트 제거로 실제 코드 무조건 컴파일 복원. 파일: FirebaseAuthService.cs / LoginBootstrapper.cs(GPGS 가드 2곳) / mainTemplate.gradle. 사용자 로컬 임포트(Firebase 13.11.0+GPGS 2.1.0) 후 PASS. 커밋 `4fe1cf0`. 잔여: 에디터 "Firebase 초기화 실패" 런타임 로그(별도).
- 최신 전체 현황은 `Assets/_Project/Docs/PROJECT_STATUS.md` 참조.

## 프로젝트 현재 상태 (2026-06-23)

### 2026-06-23 완료
- **코드 정리(클린업) Phase 1** — 히스토리성 주석 및 폐기 코드 제거
  - 약 30개 파일에서 `[2026-XX-XX]`/`[Phase X]` 형식 이력 라벨 + 구방식→현재방식 전환 설명 주석 제거
  - 폐기 코드 블록 제거: `GameBootstrapper.cs` `_enableAI` 블록(주석 처리 코드+메모), `_confirmPopup` 전환 설명 블록
  - `NetworkGameFlow.cs` 빈 섹션 헤더 제거
  - `GameBootstrapper.Setup.cs` 중복 RaceId 배열 → `refundRaces` 지역 변수 1개로 통합 (원소·순서 동일 → 환불 캐시 결과 불변)
  - 런타임 동작 변경 없음(순수 주석/폐기코드 정리). 미래 사용 의도 주석 미발견(플래그 없음)
  - 브랜치 `claude/code-refactor-cleanup-jsa24o`. task: `_Tasks/2026-06-23/00_00_코드정리-클린업/`
  - 구조 변경(switch→Dictionary 등)은 **Phase 2**로 별도 진행 예정
- 참고: 이 메모리의 하위 날짜 섹션들은 과거 이력. 최신 전체 현황은 `Assets/_Project/Docs/PROJECT_STATUS.md` 참조

## 프로젝트 현재 상태 (2026-04-13)

### 2026-04-12~13 완료
- **유닛/건물 스탯 확정 적용** (StatsReference.md 기준)
  - Spirit/Transcendence 6종 HP/ATK/생산시간/비용 확정값 적용
  - Pistoleer MoveSpeed 0.5f 수정
  - Transcendence 건물 HP 종족별 분기 (`BuildingStats.GetMaxHp(type, RaceId)`)
  - 생산 패널/건물 배치 UI 골드 비용 숫자 표기 추가 (G 없음)
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-04-12/06_42_stats-apply/`
- **피격 시 부유 HP 텍스트**
  - FloatingHpText.cs + FloatingHpTextSpawner.cs + 프리팹 신규
  - 줌 기반 크기/위치 스케일링 (orthographicSize 기준)
  - 멀티플레이: NetworkHealthSync에서 클라이언트 재발행으로 양측 표시
  - SetupFloatingHpText 에디터 스크립트 자동화
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-04-12/18_03_floating-hp-text/`

## 프로젝트 현재 상태 (2026-04-06)

### 2026-04-06 완료
- **로비 종족 선택 UI (캐러셀 방식)**
  - 3종족(인간/정령/초월) 캐릭터 3D 미리보기 (RenderTexture + CharacterPreview 레이어)
  - DOTween 캐러셀 전환, Walk/Idle CrossFade 1초 블렌드
  - RaceId.Transcendence (자연→초월 전체 rename)
  - Pistoleer.controller Idle m_Speed 0→1 버그 수정
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-04-04/21_00_race-selection-ui/`
- **알려진 미완성 (다음 작업)**:
  - 싱글플레이 게임 시작 시 GameRaceContext.Set() 미호출 (BUG-2) — 인게임 종족 반영 미완
  - 멀티플레이 종족 전달은 NetworkGameFlow에서 처리되므로 정상

## 프로젝트 현재 상태 (2026-03-26)

### 2026-03-27 완료
- **공격 타이밍 정밀화**
  - 타격 프레임 데미지: HitFrameTime 딜레이 후 ApplyAttackDamage (Assault=0.133s, Pistoleer=0.833s, Sniper=2.0s)
  - 타겟 고정(Target Lock): IsInRange 체크 제거 — 공격 모션 시작 시 타겟 확정
  - 쿨다운 통일: UnitView.Update() 제거 → GameBootstrapper.Update() → TickCooldowns()
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-27/11_00_attack-timing-precision/`
- **이동 전 회전 타이밍 수정 (Rotate-then-Move 완성)**
  - 원인: DOTween(Update) vs NetworkUnit.LateUpdate 충돌 — LateUpdate가 DOTween rotation 덮어씌움
  - `NetworkUnit.cs`: `_isPreRotating` 플래그 + `SetPreRotating()` + LateUpdate `!_isPreRotating` 조건
  - `NetworkCombatController.cs`: `SetPreRotating(true)` + DORotate `.OnComplete(() => SetPreRotating(false))`
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-27/10_00_rotation-timing-fix/`

### 2026-03-26 완료
- **유닛 NGO NetworkObject 전환 + 이동/전투 동기화**
  - `NetworkUnit.cs` (신규): 유닛 프리팹 루트 NetworkBehaviour — unitId NetworkVariable, Red 클라이언트 좌표 보정(LateUpdate), 위치 델타 기반 회전
  - `UnitFactory.cs`: 멀티+서버 → NetworkObject.Spawn() (SetParent 없음 — NGO 제약)
  - `NetworkCombatController.cs`: TurnToFaceClientRpc 추가, StartWalkWithRetry 1초 재시도
  - `NetworkUnitMovementController.cs`: 클라이언트 예측 제거, SyncMovementClientRpc 제거
  - `UnitView.cs`: _isWalkPending 패턴, AttackWait 탈출 후 공격 완료 대기, 첫 스텝 회전 대기
  - `GameEvents.cs`: UnitFacingChangedEvent(UnitId, YAngle, RotationDuration) 추가
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-26/15_08_network-unit-combat-sync/`

---

## 이전 상태 (2026-03-19)

### 2026-03-19 완료
- **카메라 줌 DOTween 보간**
  - `CameraController.cs`: HandleZoom() 즉시 적용 → DOTween.To + Ease.OutCubic 보간
  - `_targetZoom` 누적 방식 — 연속 스크롤 시 자연스러운 목표값 갱신
  - `_zoomDuration` (SerializeField, default=0.25f) Inspector 조정 가능
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-19/camera-zoom-smooth/`

### 2026-03-18 완료
- **랜덤매칭 재경기 지원**
  - `GameEndUI.SetupRematchButton()`에서 `isRandomMatch==true` 버튼 숨김 분기 제거
  - 랜덤매칭도 커스텀게임과 동일한 양측 동의 재경기 흐름 (추가 RPC 불필요)
- **건물 인근 타일 이동/공격 불가 버그 수정**
  - `HexPathfinder.FindPath()`: goal blocked 체크 제거 — ClaimedTile 교착 상태 해소
  - `UnitCombatUseCase.FindFirstEnemyTarget()`: maxDist에 Epsilon=0.05f 추가 — 인접 경계 부동소수점 오차 방지
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-17/building-adjacent-movement-fix/`

### 2026-03-17 완료
- **커스텀게임 재경기 시스템**
  - 싱글=즉시 재시작(변경 없음), 랜덤/커스텀=양측 동의 재경기 (2026-03-18 랜덤 통합)
  - `NetworkGameManager.IsRandomMatchmaking` 속성 추가
  - `NetworkGameEndController` RPC 재경기 시스템 (Request/Accept/Decline, targeted ClientRpc)
  - `RematchRequestPopup.cs` 신규 (`Presentation/UI/Common/`), `RematchPopupBuilder.cs` 에디터 스크립트
  - 레이스 컨디션 처리: 서버 `_rematchRequesterId` 상태값
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-17/18_45_custom-game-rematch/`
- **멀티플레이 로비 복귀 버그 수정**
  - 근본 원인: `NetworkGameEndController._lobbySceneName` Inspector="Game" (코드 기본값 아님)
  - RPC 로비 복귀 메서드 4개 제거 → 각 클라이언트 독립 로컬 처리로 설계 변경
  - `GameEndUI.cs`: `ReturnToLobby()` + `CountdownCoroutine()` (30초 자동 복귀) 추가
  - Inspector 연결 필요: `_countdownText` (TextMeshProUGUI)
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-17/16_20_back-to-lobby-bug/`
- **전역 로딩 스크린 구현 완료**
  - `LoadingScreen.cs` (`Presentation/UI/Common/`): 싱글턴, DontDestroyOnLoad, CanvasGroup DOFade
  - Lobby 씬 Canvas(SO:100) 배치 — Background/Spinner/StatusText 구조
  - 싱글플레이: `await Task.Delay(2000)` 후 씬 전환 (최소 2초 표시)
  - 커스텀/랜덤매칭: `LoadGameScene()` 직전 Show(), `sceneLoaded` 자동 Hide()
  - `NetworkGameManager.StartMatchmakingAsync`에 `onMatchFound` 콜백 추가
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-16/loading-screen/`

### 2026-03-16 완료
- **랜덤 매칭 후 게임 씬 전환 버그 수정**
  - `MatchmakerManager.DetermineIsHostAsync`: `string.GetHashCode()` → `GetStableHash()` (polynomial hash) 교체
  - `NetworkGameManager.HostGameAsync`: `OnClientConnectedCallback` 등록을 `StartNetworkHost()` 이전으로 이동
  - task 문서: `Assets/_Project/Docs/_Tasks/2026-03-16/random-matchmaking-game-start-bug/`

### 이전 상태 (2026-03-14)
- 싱글플레이 코어 루프 완성 (헥스 그리드, 전투, 건물, 생산, 승패)
- 멀티플레이 Phase 1~8 완성 + 자동생산 멀티플레이 완료 (ToggleAutoServerRpc + AutoProductionChangedClientRpc)
- **2D→3D 전환 완료 (Phase 0~5)**
  - Phase 0: NetworkGameEndController 씬명 하드코딩 수정 ("SampleScene" → "Game")
  - Phase 1: XY→XZ 좌표계 전환 (HexMetrics, ViewConverter, CameraController, GameBootstrapper, InputHandler)
  - Phase 2: Sprite→Mesh 렌더링 전환 (BuildingFactory sortingOrder 제거, UnitFactory/UnitView AnimationData 제거, FrameAnimator.cs 삭제)
  - Phase 3: 카메라 55도 틸트 적용 (CameraController _tiltAngle=55f, GameBootstrapper Z오프셋 보정)
  - Phase 4: 3D 헥스 타일 제작 완료 (ProBuilder + Shader Graph)
    - mat_tile_top: SG_HexTile (Object Space Position 기반 HexBorder, #BCBCBC/#3A3A3A, 두께 0.02)
    - mat_tile_side: #3A3A3A 단색
  - Phase 4 추가 확인: Pistoleer 프리팹 `Assets/_Project/Prefabs/Units/Unit_Pistoleer.prefab` 완성 (애니메이션 작동 확인)
  - Phase 5: 건물 3D 모델 제작 완료 (2026-03-01)
    - Castle, Barracks, MiningPost, GoldMineTile 프리팹 완성 (Meshy.ai Image-to-3D)
    - 프리팹 구조: 빈 루트(0,0,0) + 자식 FBX 메시(Scale/Rotation/Y 보정)
    - HexGridRenderer 금광 타일 3D 전환 완료

- **2026-03-07 복구/버그 수정 작업 (git restore 사고로 소실된 작업 복원)**
  - **공격 방향 transform 기반 복구**: UnitView.CalculateAttackAngle — transform.position Atan2, _meshYOffset=30f 보정
  - **AttackCooldown 시스템 복구**: UnitData.AttackCooldown/AttackCooldownRemaining, UnitFactory 클립 길이 자동 설정
  - **IEntityPositionProvider 재구현**: git restore로 소실 → 재구현 완료
    - `Application/Interfaces/IEntityPositionProvider.cs` (신규)
    - `Infrastructure/UnitWorldPositionProvider.cs` (신규) — UnitFactory/BuildingFactory.GetObject().transform.position
    - `UnitCombatUseCase`: 월드좌표 Vector3.Distance 기반 사거리 판정, null/zero 시 HexCoord 폴백
    - `GameBootstrapper.CreateUseCases()`: UnitWorldPositionProvider 생성 후 UnitCombatUseCase에 주입
    - 임계값: `AttackRange * HexMetrics.TileHeight` (2026-03-14 epsilon 제거 — 조기 공격으로 타일 점령 안 되는 버그 수정)
  - **HexTileView 타일 색상 버그 수정**: 옆면 → 윗면 색상 변경
    - `renderer.material`(index 0=side) → materials 배열 순회, SG_HexTile 셰이더 탐색 후 캐시
    - `material.color` → `material.SetColor("_BaseColor", ...)` (SG_HexTile Shader Graph 프로퍼티)
  - **_Tasks 폴더 구조 변경**: `YYYY-MM-DD_작업명/` → `YYYY-MM-DD/HH_MM_작업명/` (날짜별 분류)

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
1. 관련 파일 경로 (절대 경로, `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/Assets/...`) — 탐색 비용 절감
2. Clean Architecture 레이어 규칙 (Domain이 Core 참조 불가 등)
3. NGO API 제약 명시 (ServerRpc/ClientRpc 이름 규칙, NetworkBehaviour=Infrastructure만, RPC 파라미터 직렬화 타입)
4. 현재 프로젝트 상태 요약
5. 해당 에이전트 MEMORY.md 경로

## 작업 완료 후 메모리 업데이트 체크리스트
- [ ] game-programmer MEMORY.md: 새 파일/클래스/API 매핑 추가
- [ ] qa-tester MEMORY.md: 새 취약 지점, 테스트 체크리스트 항목 추가
- [ ] game-design-lead MEMORY.md: 구현 완료 항목 이동, 미결 항목 갱신
- [ ] 메인 MEMORY.md (C:/Users/rmsep/.claude/...): 아키텍처 결정사항 반영
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
