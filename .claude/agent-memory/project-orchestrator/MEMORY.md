# Project Orchestrator Memory — Hexiege

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음, 모든 서브에이전트 포함)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 attack direction 작업 전체 삭제 (복구 불가)
- 서브에이전트(game-programmer 등)에 작업 위임 시에도 이 규칙을 반드시 명시할 것
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## 프로젝트 현재 상태 (2026-03-02)
- 싱글플레이 코어 루프 완성 (헥스 그리드, 전투, 건물, 생산, 승패)
- 멀티플레이 Phase 1~8 완성 (Lobby/Relay/NGO, 건물/생산/이동/전투/HUD/재접속)
- **2D→3D 전환 완료 (Phase 0~3)**
  - Phase 0: NetworkGameEndController 씬명 하드코딩 수정 ("SampleScene" → "Game")
  - Phase 1: XY→XZ 좌표계 전환 (HexMetrics, ViewConverter, CameraController, GameBootstrapper, InputHandler)
  - Phase 2: Sprite→Mesh 렌더링 전환 (BuildingFactory sortingOrder 제거, UnitFactory/UnitView AnimationData 제거, FrameAnimator.cs 삭제)
  - Phase 3: 카메라 55도 틸트 적용 (CameraController _tiltAngle=55f, GameBootstrapper Z오프셋 보정)
  - Phase 4: 3D 헥스 타일 제작 완료 (ProBuilder + Shader Graph)
    - mat_tile_top: SG_HexTile (Object Space Position 기반 HexBorder, #BCBCBC/#3A3A3A, 두께 0.02)
    - mat_tile_side: #3A3A3A 단색
  - Phase 4 추가 확인: Pistoleer 프리팹 `Assets/_Project/Prefabs/Units/Unit_Pistoleer.prefab` 완성 (애니메이션 작동 확인)
  - Phase 5: 건물 3D 모델 제작 완료 (2026-03-01)
  - **버그 수정: 2타일 시각적 공격 거리 버그 해결 (2026-03-02, Option C-Clean)**
    - IEntityPositionProvider (Application) + UnitWorldPositionProvider (Infrastructure) 신규 추가
    - UnitCombatUseCase: 월드좌표 기반 거리 판정, provider 미등록 시 헥스 좌표 fallback
    - UnitView/UnitFactory/GameBootstrapper: positionProvider 주입 체인 연결
    - Castle, Barracks, MiningPost, GoldMineTile 프리팹 완성 (Meshy.ai Image-to-3D)
    - 프리팹 구조: 빈 루트(0,0,0) + 자식 FBX 메시(Scale/Rotation/Y 보정)
    - HexGridRenderer 금광 타일 3D 전환 완료
    - HexTileView 팀 색상 시스템 수정 완료
      - SG_HexTile Shader Graph에 `_BaseColor` Blackboard 프로퍼티 추가
      - HexTileView 컴포넌트를 새 3D 타일 프리팹에 수동 추가 필요 (ProBuilder 자동 추가 안됨)
      - `material.color` → `material.SetColor("_BaseColor", ...)` 로 변경
      - SG_HexTile이 materials[1](top), Lit이 materials[0](side) 순서 — 셰이더 이름으로 자동 탐색

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
- 유닛 Animator 파라미터: IsWalking(bool), IsDead(bool), Attack(trigger)
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
1. 관련 파일 경로 (절대 경로, `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/Assets/...`)
2. Clean Architecture 레이어 규칙 (Domain이 Core 참조 불가 등)
3. 현재 프로젝트 상태 요약
4. 해당 에이전트 MEMORY.md 경로

## 작업 완료 후 메모리 업데이트 체크리스트
- [ ] game-programmer MEMORY.md: 새 파일/클래스/API 매핑 추가
- [ ] qa-tester MEMORY.md: 새 취약 지점, 테스트 체크리스트 항목 추가
- [ ] game-design-lead MEMORY.md: 구현 완료 항목 이동, 미결 항목 갱신
- [ ] 메인 MEMORY.md (C:/Users/rmsep/.claude/...): 아키텍처 결정사항 반영
- [ ] project-orchestrator MEMORY.md: 현재 상태 요약 갱신

## 사용자 워크플로우 선호사항
- 사용자는 총괄(project-orchestrator)에게 요청 → 총괄이 각 에이전트에게 분배
- **규모/복잡도 관계없이 모든 작업은 시작 전 반드시 계획을 사용자에게 검토받을 것 — 승인 없이 바로 작업 절대 금지**
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
- BuildFailedClientRpc/EnqueueFailedClientRpc: UI 피드백 미구현 (TODO 로그만)
- InputHandler 이동: NetworkUnitMovementController 경유가 아닌 직접 UseCase 호출 (싱글플레이 경로 잔재)
  - `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` L261 부근
- NetworkProductionController 자동생산 토글: ProductionPanelUI 멀티플레이 롱프레스 미지원
- 생산 UI 클라이언트 큐 동기화: SyncQueueStateClientRpc가 큐 추가 시 즉시 전파 안됨
- 재접속 기능: 실제 재접속 흐름 없음 (30초 대기 후 ForceWin만 구현)

## 3D 전환 시 수정된 파일 (참고)
- Phase 1: `HexMetrics.cs`, `ViewConverter.cs`, `CameraController.cs`, `GameBootstrapper.cs`, `InputHandler.cs`
- Phase 2: `BuildingFactory.cs`, `UnitFactory.cs`, `UnitView.cs` (삭제: `FrameAnimator.cs`, `UnitAnimationData.cs`, `PistoleerAnimData.asset`)
- Phase 3: `CameraController.cs` (tilt), `GameBootstrapper.cs` (Z오프셋)
- Phase 4: Meshy.ai 에셋 통합 예정
