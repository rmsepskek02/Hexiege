# Game Programmer Agent Memory

## CRITICAL — GIT 명령 절대 금지
- **모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## CRITICAL — 구현 시 필수 확인 제약

### 레이어 제약
- Domain: `using Hexiege.Core` 절대 금지 → HexOrientationContext 등 정적 홀더 패턴
- NetworkBehaviour: Infrastructure 레이어에만 (Presentation/Application 금지)
- Application: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 패턴
- GameBootstrapper = 유일한 의존성 조합 루트
- Assembly Definition 없음 — 네임스페이스 규약만

### NGO API 제약
- ServerRpc/ClientRpc 메서드명: 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함
- NGO 2.9.2, Enable Scene Management = ON
- NetworkBehaviour는 씬에 NetworkObject로 배치해야 RPC 작동
- RPC 파라미터: 직렬화 가능 타입만 (INetworkSerializable 또는 기본 타입/enum)
- 클라이언트 전용 분기: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`

## 최근 작업

### 코드 정리 (2026-03-20) ✅ 테스트 완료
- **TeamAssigner.cs 삭제**: Player Prefab=None으로 스폰 안 됨, NetworkGameFlow로 완전 대체 확인 후 삭제
- **LocalPlayerTeam.cs 주석 정리**: "TeamAssigner에서 호출" → "NetworkGameFlow에서 호출" (5곳)
- **NetworkGameFlow.cs 주석 정리**: L12 "TeamAssigner 준비 대기" → "IsHost 기반으로 팀 직접 결정"
- **GameBootstrapper.cs IsNetworkMode() 헬퍼 추출**: `NetworkManager.Singleton != null && (IsHost || IsClient)` 4곳 중복 → private 메서드 통합

### 싱글플레이 ViewConverter 초기화 버그 수정 (2026-03-20) ✅ 테스트 완료
- **증상**: Red팀 싱글플레이에서 내 진영이 화면 하단이 아닌 상단에 표시
- **원인**: `ViewConverter.Reset()`이 LocalPlayerTeam.Current 무시하고 항상 Blue 관점 고정
- **수정**: `GameBootstrapper.LoadMap()` — `ViewConverter.Reset()` 제거, `ApplyConfig()` 직후 LocalPlayerTeam 기반 Setup:
  ```csharp
  if (!isNetworkMode)
  {
      Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
      bool isRed = (LocalPlayerTeam.Current == TeamId.Red);
      ViewConverter.Setup(isRed, mapCenter);
  }
  ```
- **주의**: `ApplyConfig()` 이후에 호출해야 HexMetrics 준비 완료 후 GridCenter 계산 가능
- **카메라 초기 위치는 변경 없음** — 맵 중앙 유지 (SetCameraStartPositionForTeam 호출 금지)

### 카메라 줌 DOTween 보간 (2026-03-19) ✅ 테스트 완료
- **CameraController.cs**: HandleZoom() 즉시 적용 → DOTween 보간으로 교체
  - `_targetZoom` (float): 입력 시 Clamp된 목표값 누적
  - `_zoomTween` (Tweener): Kill() 후 새 Tween 시작 — 연속 스크롤 시 부드럽게 목표 갱신
  - `DOTween.To(() => _cam.orthographicSize, x => _cam.orthographicSize = x, _targetZoom, _zoomDuration).SetEase(Ease.OutCubic)`
  - `_zoomDuration` (SerializeField, default=0.25f): Inspector 조정 가능
  - `Awake()`에서 `_targetZoom = _cam.orthographicSize` 초기화
  - `OnDestroy()`에서 `_zoomTween?.Kill()` 정리
  - `using DG.Tweening` 추가
- ClampPosition()은 매 프레임 orthographicSize 읽으므로 수정 불필요

### 건물 인근 이동/공격 불가 버그 수정 (2026-03-18) ✅ 테스트 완료
- **HexPathfinder.cs**: `FindPath()` goal blocked 체크 제거 — 목표 타일이 ClaimedTile에 선점되어도 경로 탐색 가능
  - 이전: `if (blockedCoords.Contains(goal)) return null;` → 인근 타일 모두 선점 시 교착 상태
  - 이후: blocked는 경로 중간 타일에만 적용, 목표 도착 충돌은 ProcessStep에서 처리
- **UnitCombatUseCase.cs**: maxDist에 `Epsilon=0.05f` 추가
  - Pistoleer maxDist(0.866) = FlatTop 인접 거리(0.866) 경계 케이스 → 부동소수점 오차로 공격 실패
  - `float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;`

### 랜덤매칭 재경기 지원 (2026-03-18) ✅
- **GameEndUI.cs**: `SetupRematchButton()`에서 `isRandomMatch==true`일 때 버튼 숨기는 분기 제거
  - 랜덤매칭도 커스텀게임과 동일 흐름: 양측 동의 재경기 팝업 + NGO SceneManager.LoadScene("Game")

### 커스텀게임 재경기(Rematch) 시스템 (2026-03-17) ✅ 테스트 완료
- **NetworkGameManager.cs**: `_isRandomMatchmaking` bool 필드 + `IsRandomMatchmaking` 속성 추가
  - StartMatchmakingAsync → true, CancelMatchmakingAsync/DisconnectAsync → false
- **NetworkGameEndController.cs**: 재경기 RPC 시스템 전면 교체
  - `AnnounceWinnerClientRpc(int, bool isRandomMatch)` — 파라미터 2개로 변경
  - `_rematchRequesterId` (ulong.MaxValue=없음) — 첫 요청자 추적, 양측 요청 시 즉시 재경기
  - RPC: RequestRematchServerRpc, AcceptRematchServerRpc, DeclineRematchServerRpc
  - ClientRpc: NotifyRematchRequestedClientRpc(targeted), NotifyRematchDeclinedClientRpc(targeted)
  - `StartRematch()`: NGO SceneManager.LoadScene("Game") — 네트워크 유지 상태 씬 재로드
  - `_lobbySceneName` 제거, `OnMultiplayerRestart()` 제거
  - `_rematchRequestPopup` SerializeField 추가 (Inspector 연결 필요)
- **GameEndUI.cs**: `OverrideRestartForMultiplayer()` → `SetupRematchButton(bool, Action)` + `RestoreRematchButton()` 교체
  - `_restartButtonText` SerializeField 추가 (Inspector 연결 필요)
  - ~~랜덤매칭: 다시하기 버튼 숨김~~ → 2026-03-18 제거, 랜덤매칭도 재경기 지원
  - 커스텀게임: 요청/대기/복원 UI 상태 관리
- **RematchRequestPopup.cs** (신규): `Presentation/UI/Common/` — `_overlay`+수락/거절 팝업+거절 알림 팝업
  - Inspector 연결 필요: _overlay, _requestPanel, _acceptButton, _declineButton, _declinedPanel, _declinedConfirmButton
  - **루트 오브젝트는 Active 유지 필수** — FindFirstObjectByType은 비활성 오브젝트 탐색 불가
  - Hide()/Show*()에서 _overlay도 함께 제어 (overlay 별도 필드로 관리)
- **FindFirstObjectByType 교훈**: 비활성 오브젝트 포함 탐색 시 `FindObjectsInactive.Include` 인자 필요

### 멀티플레이 로비 복귀 버그 수정 (2026-03-17)
- **근본 원인**: `NetworkGameEndController._lobbySceneName` Inspector="Game" → 게임 씬 재로드
- **GameEndUI.cs**: `ReturnToLobby()` (NGM.Shutdown + LoadScene("Lobby")), `CountdownCoroutine()` (WaitForSecondsRealtime 기반 30초)
- Inspector 연결 필요: `_countdownText` (TextMeshProUGUI), `_autoReturnSeconds` (default=30f)

### 전역 로딩 스크린 구현 (2026-03-17)
- `LoadingScreen.cs` (`Presentation/UI/Common/`): 싱글턴, DontDestroyOnLoad, CanvasGroup DOFade 페이드 인/아웃
- `BattleViewModel.cs`: 싱글플레이 `LoadSingleplayScene()` → async void + `await Task.Delay(2000)` + Show/Hide
- 커스텀 호스트/참가: `LoadGameScene()` 직전 `LoadingScreen.Instance?.Show()`
- 랜덤매칭: `NetworkGameManager.StartMatchmakingAsync`에 `onMatchFound` 콜백 추가 → matchId 확보 직후 Show()
- sceneLoaded 이벤트로 모든 케이스 자동 Hide() (NGO 씬 전환 포함)

### 랜덤 매칭 버그 수정 (2026-03-16) — [random-matching-bugfix.md](random-matching-bugfix.md)
- string.GetHashCode() 크로스-프로세스 비결정성 → GetStableHash() 대체
- NetworkGameManager: OnClientConnectedCallback 등록을 StartNetworkHost() 이전으로 이동

### Animation Event 타격 반응 (2026-03-14) — [rendering-and-animation.md](rendering-and-animation.md)
- AnimationEventRelay → UnitView.OnAttackHit() → scale punch 시각 효과

### 유닛 확정 스탯 (2026-03-14) — [unit-stats-and-combat.md](unit-stats-and-combat.md)
- Pistoleer/Assault/Sniper 3종 스탯 확정, AttackRange int→float 변경

## 토픽 파일 인덱스

### 네트워크
- [network-infra.md](network-infra.md) — Phase 1~8 상세 (UGS, NGO, 동기화, UI/UX, 팀 할당, 승패)
- [network-todo.md](network-todo.md) — 네트워크 미완성 항목
- [random-matching-bugfix.md](random-matching-bugfix.md) — 2026-03-16 랜덤 매칭 버그 수정

### 전투 & 유닛
- [unit-stats-and-combat.md](unit-stats-and-combat.md) — 스탯, IEntityPositionProvider, 쿨다운, 클라이언트 시각 동기화
- [combat-fixes.md](combat-fixes.md) — ClaimedTile 기반 공격 위치 보정, UnitView 부드러운 회전
- [attack-direction-refactor.md](attack-direction-refactor.md) — 공격 방향 리팩터링 (2D→3D)

### 렌더링 & 뷰
- [rendering-and-animation.md](rendering-and-animation.md) — UnitView 애니메이션, Shader Graph, HexTileView, 팀 프리팹
- [3d-transition.md](3d-transition.md) — XZ 좌표계 전환, 렌더링 전환, Phase별 수정 파일
- [camera-and-view.md](camera-and-view.md) — 카메라 틸트, ViewConverter, 경계 클램프, 건물 위치 버그

### 게임플레이
- [gameplay-systems.md](gameplay-systems.md) — 랠리포인트, 초상화 동적 업데이트

## 핵심 패턴 요약

### 정적 홀더 패턴 (레이어 간 의존성 우회)
- `HexOrientationContext` — Domain에서 Core의 Orientation 접근
- `NetworkContext` — Application에서 NetworkManager 상태 접근
- `LocalPlayerTeam` — 현재 플레이어 팀 (싱글=Blue, 네트워크 시 갱신)
- `ViewConverter` — Red팀 좌표/방향 반전

### GameBootstrapper Start() 분기
- NetworkManager null 또는 IsHost/IsClient=false → 싱글플레이 (LoadMap 즉시)
- 네트워크 → 맵 로드 건너뜀, NetworkGameFlow가 StartNetworkGame() 대기
- C# LangVersion 9.0 (switch expression 사용 가능)

### 팀 매핑
- TeamId: Neutral=0, Blue=1, Red=2
- Host→Blue, Client→Red
- TeamAssigner는 삭제됨 (2026-03-20) — NetworkGameFlow.WaitForTeamAndSendReady()에서 IsHost?Blue:Red로 직접 할당

### 동기화 타이밍
- NetworkSync 스폰 시 HexGrid/ResourceUseCase null 가능 → null 방어 필수
- ResourceUseCase 생성자는 OnResourceChanged 미발행 → SyncInitialGold() 필요
- ViewConverter.Setup()은 LoadMap() 이전에 호출해야 함

### 유닛 애니메이션 핵심
- Animator.Play() 직접 호출 (트랜지션 우회)
- 파라미터: IsDead(bool) 1개만
- Idle = Walk speed=0
- Root Motion 반드시 OFF
