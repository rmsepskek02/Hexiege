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

### 중립 광산 오브젝트 표시 제어 (2026-04-08) ✅ 싱글 실기 완료

**변경 파일**:
- `Presentation/Grid/HexGridRenderer.cs` — `_goldMineObjects` List→Dictionary, `RenderGoldMines()` 초기 숨김, `HideGoldMine()`/`ShowGoldMine()` 추가, `SubscribeGoldMineEvents()` 추가
- `Application/UseCases/BuildingPlacementUseCase.cs` — `RemoveBuilding()` 내 MiningPost 파괴 시 타일 Owner Neutral 복원 + OnTileOwnerChanged 발행

**핵심 설계 결정**:
- 초기 숨김 판별: `RenderGoldMines()` 내 `tile.Owner != TeamId.Neutral` 조건 (PlaceMiningPostDirect 이후 호출 순서 보장)
- 이벤트 구독: `OnBuildingPlaced` → HideGoldMine / `OnEntityDied(MiningPost)` → ShowGoldMine
- 타일 소유권 복원: `RemoveBuilding()`에서 처리 — 싱글(UnitCombatUseCase)/멀티(NetworkCombatController) 모두 이 메서드를 거치므로 단일 수정으로 양쪽 커버

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/23_45_goldmine-hide/`

### 종족 인게임 적용 (2026-04-07) ✅ 싱글/멀티 실기 완료

**변경 파일**:
- `Infrastructure/Factories/UnitFactory.cs` — 종족별 6세트 프리팹(`_humanBlue/Red`, `_spiritBlue/Red`, `_transcendenceBlue/Red`), GameRaceContext 조회 후 switch 선택, 오브젝트명=`{prefab.name}_{id}`
- `Infrastructure/Factories/BuildingFactory.cs` — 동일 종족별 6세트 패턴, BuildingTeamPrefabSet에 `miningPost` 필드 추가
- `Bootstrap/GameBootstrapper.cs` — 싱글플레이 Start()에 `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` 추가
- `Editor/SetupUnitFactoryPrefabs.cs` (신규) — 유닛 18개 + 건물 12개 자동 프리팹 연결 에디터 메뉴

**핵심 설계 결정**:
- GameRaceContext(Infrastructure 정적 홀더)를 UnitFactory/BuildingFactory에서 직접 참조 — 레이어 위반 없음
- UnitData에 Race 필드 추가하지 않음 — 스폰 시점에 GameRaceContext에서 직접 조회
- MiningPost: BuildingTeamPrefabSet.miningPost 필드로 종족별 분기

**건물 종족 매핑 (확정)**:
| BuildingType | Human | Spirit | Transcendence |
|---|---|---|---|
| Castle | Building_Castle | Building_SpiritNexus | Building_ElderTree |
| Barracks | Building_Barracks | Building_SummoningAltar | Building_HunterPlant |
| MiningPost | Building_MiningPost | Building_ManaRift | Building_FungalNode |

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/09_00_faction-ingame-apply/`

### 전투 애니메이션 시스템 전면 재정비 (2026-04-03~04) ✅ 완료

**핵심 변경 파일**:
- `NetworkCombatController.cs` — 3-신호 RPC, TickCombat elapsed 수정, _combatAnimationSent, ExecuteAttack 동시 호출
- `UnitView.cs` — Walk CrossFade 1회 제한, _attackToWalkBlend, StopCombatAnimation 빈 메서드
- `NetworkUnit.cs` — WaitForUnitId 폴링 → OnValueChanged 콜백 교체
- `UnitCombatUseCase.cs` — TryAttack 네트워크 완전 차단 (HOST 이중 데미지 방지)
- `UnitStats.cs` — GetAttackCooldown 실제 클립 길이로 업데이트 (Assault=0.2, Pistoleer=2.0, Sniper=3.0)

**핵심 설계 결정**:
- StartCombatClientRpc: OnUnitEnteredCombatHandler 단독 전송 (TickCombat에서 제거)
- AttackCooldown = 클립 길이 — Animator 상태 읽기 없이 순수 타이머로 사이클 판단
- StopCombatAnimation() = 빈 메서드 — Walk는 StartWalkAnimationClientRpc 타이밍에만 전환
- `_combatAnimationSent` HashSet — TickCombat/코루틴 실행 순서 경쟁 조건 방지용 RPC 전송 추적

**버그 패턴 교훈**:
- TickCombat(Update)은 코루틴(yield return null)보다 먼저 실행 → 같은 프레임에 Dictionary 먼저 등록 가능
- RPC 전송 여부 추적은 타겟 추적 Dictionary와 반드시 분리
- ExecuteAttack을 핸들러에서 즉시 호출해야 서버 공격 사이클 T=0 = 애니메이션 루프 T=0 동기화

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/10_00_combat-animation-overhaul/`

### 유닛 NGO NetworkObject 전환 + 이동/전투/회전 동기화 (2026-03-26~29) ✅ 완료

**핵심 설계 결정 (2026-03-29 최종)**:
- 유닛 위치 동기화: NGO NetworkTransform (서버 position → 클라이언트 자동 보간)
- **유닛 회전 동기화: NetworkTransform SyncRotAngleY=true (서버 즉시 스냅 → 클라이언트 보간)**
- Walk/공격/사망 동기화: ClientRpc (이벤트 기반)
- Red 클라이언트 좌표+회전 보정: NetworkUnit.LateUpdate() (위치 반전 + Y축 +180°)
- NGO NetworkObject 부모 제약: 씬 루트에 생성 (일반 GameObject 하위 불가)
- 클라이언트 등록 타이밍: WaitForUnitId 폴링 + ApplyStartWalkWithRetry로 등록 지연 대응

**폐기된 패턴 (2026-03-29)**:
- ~~클라이언트 LateUpdate 델타 기반 회전 (Atan2 + RotateTowards)~~ → NetworkTransform rotation 동기화로 대체
- ~~TurnToFaceClientRpc + DORotate 보간~~ → 서버 즉시 스냅 + NetworkTransform 보간으로 대체
- ~~_isPreRotating / SetPreRotating / SetAttackRotating~~ → 전면 제거
- ~~_isWalkPending~~ → 공격 중 Walk 무시 가드(`if (_attackCoroutine != null) return`)로 교체
- ~~HasReceivedTurnToFace / MarkTurnToFaceReceived~~ → 전면 제거
- ~~ResetMovementTracking / ResetPositionTracking~~ → 전면 제거
- ~~UnitView의 DOKill/DORotate~~ → Quaternion.Euler 즉시 스냅으로 교체 (using DG.Tweening 제거)
- ~~GameEvents.OnUnitFacingChanged / UnitFacingChangedEvent~~ → 전면 제거
- ~~NetworkCombatController.TurnToFaceClientRpc~~ → 전면 제거

**이중 보간 문제 교훈**:
서버 DORotate(0.3초) + NetworkTransform 보간(0.1초) = ~1초 딜레이.
서버에서 즉시 스냅하면 NetworkTransform 보간만 적용되어 자연스러운 회전.

### 공격 타이밍 정밀화 (2026-03-27) ✅ 실기 테스트 완료

**구현 내용**:
- **타격 프레임 데미지**: 서버가 애니메이션 RPC 즉시 전송 → HitFrameTime 후 데미지 적용
- **타겟 고정(Target Lock)**: ApplyAttackDamage에서 IsInRange 체크 제거 — 공격 모션 시작 시 타겟 확정
- **쿨다운 통일**: UnitView.Update() 쿨다운 제거 → GameBootstrapper.Update() → TickCooldowns()

**신규 메서드 (UnitCombatUseCase)**:
- `TryFindTarget(UnitData)`: 타겟 탐색만, 데미지/쿨다운 없음 (멀티플레이 서버용)
- `ApplyAttackDamage(UnitData, int, bool)`: 딜레이 후 호출, IsAlive만 재확인 (IsInRange 없음)
- `TickCooldowns(float dt)`: 싱글플레이 전용 일괄 쿨다운 감소
- `FindTargetById(int, bool)`: Id로 Units/Buildings Dictionary 탐색

**HitFrameTime 값 (UnitStats.GetHitFrameTime)**:
- Assault: 0.133f (0:04, 4프레임/30fps)
- Pistoleer: 0.833f (0:25, 25프레임/30fps)
- Sniper: 2.000f (2:00)

**NetworkCombatController.TickCombat() 변경**:
- TryAttack() → TryFindTarget() 교체
- 성공 시: RPC 즉시 전송 + 쿨다운 리셋 + DelayedAttackDamage 코루틴 시작

**DelayedAttackDamage 코루틴**:
- HitFrameTime > 0: WaitForSeconds(delay)
- HitFrameTime = 0: yield return null (최소 1프레임 안전망)
- 이후 ApplyAttackDamage() 호출

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-03-27/11_00_attack-timing-precision/`

### 이동 전 회전 타이밍 수정 (2026-03-27) ✅ 실기 테스트 완료

**문제**: DOTween(Update) vs NetworkUnit.LateUpdate 충돌 — LateUpdate가 매 프레임 DOTween rotation을 덮어씌워 프리-회전 무효화
**해결**: `_isPreRotating` 플래그로 DORotate 실행 중 LateUpdate 델타 회전 차단

**수정 파일**:
- `Infrastructure/Network/NetworkUnit.cs`:
  - `_isPreRotating` (bool) 필드 추가
  - `SetPreRotating(bool)` public 메서드 추가
  - `ResetMovementTracking()`에 `_isPreRotating = false` 안전망 추가 (DOKill 중단 시 플래그 고착 방지)
  - LateUpdate 델타 회전 조건: `if (!_isPreRotating && _hasInitialPosition)`
- `Infrastructure/Network/NetworkCombatController.cs`:
  - `TurnToFaceClientRpc`에 `networkUnit?.SetPreRotating(true)` 추가
  - DORotate에 `.OnComplete(() => networkUnit?.SetPreRotating(false))` 추가

**핵심 패턴**: DOTween이 활성 중일 때 LateUpdate rotation 차단이 필요하면 `_isPreRotating` 패턴 사용

### Game UI Lifecycle Framework (2026-03-24) ✅ 실기 테스트 완료

**신규 파일**:
- `Presentation/UI/Core/IGameUI.cs` — UI 생명주기 인터페이스 (OnGameStarted/OnGameEnded/OnGamePaused/OnGameResumed, 모두 default 빈 구현)
- `Presentation/UI/GameUIManager.cs` — 등록/디스패치 매니저 (MonoBehaviour, [Managers] 하위 배치)

**수정 파일**:
- `Application/Events/GameEvents.cs` — OnGameStarted, OnGamePaused, OnGameResumed Subject<Unit> 추가
- `GameHudUI.cs` / `ProductionPanelUI.cs` / `BuildingPlacementUI.cs` / `GameEndUI.cs` — IGameUI 구현
- `GameBootstrapper.cs` — `_uiManager` SerializeField + LoadMap() 맨 앞에 Register/Initialize, 맨 끝에 OnGameStarted 발행
- `NetworkGameEndController.cs` — `_uiManager` 필드 추가, AnnounceWinnerClientRpc에서 `_uiManager?.NotifyGameEnded()` 호출 추가

**핵심 패턴**:
- `GameUIManager.Register()` — 중복 등록 방지 포함, LoadMap() 재호출 시 안전
- `GameUIManager.Initialize()` — CompositeDisposable로 중복 구독 방지
- `GameEndUI`는 OnGameEnded() 호출 제외 (ReferenceEquals 비교)
- **BUG-1 (멀티플레이 클라이언트 팝업 미닫힘)**: 클라이언트는 GameEvents.OnGameEnd 미발행 설계 → AnnounceWinnerClientRpc에서 직접 NotifyGameEnded() 호출로 수정

**새 UI 추가 시 체크리스트**:
1. `IGameUI` 인터페이스 구현 (필요한 메서드만 override)
2. `GameBootstrapper.LoadMap()` 앞부분에 `_uiManager.Register(새UI)` 1줄 추가
3. Inspector 참조 연결

### 반투명 배경 오버레이 구조 개선 (2026-03-23) ✅ 실기 테스트 완료

**변경 내용**:
- `AnimatedPanel.cs`: Hide() 내 `_backgroundOverlay.SetActive(false)` 타이밍 변경 — OnComplete 콜백 → Hide() 호출 즉시
- `SharedBackgroundButton.cs` (신규, `Presentation/UI/Common/`): Canvas 직속 공유 Background에 부착
  - `Register(Action onClose)` / `Unregister()` / `OnClick()` 3개 메서드
- `BuildingPlacementUI.cs` / `ProductionPanelUI.cs`: `_backgroundButton(Button)` 제거 → `_sharedBackground(SharedBackgroundButton)` 교체
  - Show()에서 `_sharedBackground?.Register(Close)`, Close()에서 `_sharedBackground?.Unregister()`

**씬 구조 변경 (Game.unity)**:
- `[UI]/Background` 하나를 ProductionPopup/BuildingPopup/GameEndPanel이 공유
- 각 팝업 자식 Background 삭제됨

### 자동/수동 생산 하이브리드 시스템 완성 (2026-03-23) ✅ 실기 테스트 완료

**핵심 설계**: AutoEntry(UnitType + IsCharged) 기반 골드 차감 시점 추적

**수정 파일**:
- `Domain/Building/ProductionState.cs` — AutoEntry 구조체, AutoEntries(List<AutoEntry>), AutoContains/AutoIndexOf 등 편의 접근자
- `Application/UseCases/UnitProductionUseCase.cs` — ToggleAutoProduction, EnqueueUnit, TryStartNext, CancelQueueAt, CanAutoEntryShowInSlot, TryPreChargeAutoEntries
- `Presentation/UI/ProductionPanelUI.cs` — UpdateQueueSlots 혼용 표시, 버튼 탭/롱프레스 분기
- `Infrastructure/NetworkProductionController.cs` — AutoEntries 참조 갱신

**핵심 패턴**:
- `CanAutoEntryShowInSlot`: AutoIndex 위치(슬롯0) 항목을 shownCount에서 **반드시 제외** (BUG-12)
  ```csharp
  for (int i = 0; i < state.AutoEntries.Count; i++)
  {
      if (i == state.AutoIndex) continue; // 슬롯0 제외
      if (state.AutoEntries[i].IsCharged) shownCount++;
  }
  ```
- `UpdateQueueSlots` 슬롯2: manualCount==1 && isNormalAutoState일 때 autoCount >= 2 필수 (BUG-13)
  - autoCount==1이면 그 항목이 슬롯0과 동일 → 슬롯2=null
- `ToggleAutoProduction` 취소 경로: 환불 없음, IsCharged=true && 슬롯1~2면 ManualQueue.Add (Rule 2)
- `TryStartNext` 자동 경로: IsCharged=false면 이 시점에 골드 차감 후 IsCharged=true 갱신
- `CompleteProduction` 자동 순환: AutoIndex 순환 **직전**에 완료된 항목의 IsCharged를 false로 리셋 (BUG-20 수정)
  ```csharp
  // AutoIndex 순환 전 IsCharged 리셋 — 다음 순환 시 골드 재차감을 위해
  var completedEntry = state.AutoEntries[state.AutoIndex];
  state.AutoEntries[state.AutoIndex] = new AutoEntry(completedEntry.Type, false);
  state.AutoIndex = (state.AutoIndex + 1) % state.AutoEntries.Count;
  ```
  - 리셋 안 하면 IsCharged=true 유지 → TryStartNext/TryPreChargeAutoEntries 모두 건너뜀 → 첫 등록 시만 골드 소모

**전역 규칙 참조**: `GameDesignDocument.md` → "생산 패널 운영 규칙" 섹션

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

### 로비 종족 선택 UI — 캐러셀 방식 (2026-04-04~06) ✅ 테스트 완료

**신규/수정 파일**:
- `Domain/Common/RaceId.cs` — enum Human=0, Spirit=1, Transcendence=2 (자연→초월 변경)
- `Infrastructure/LocalPlayerRace.cs` — 로컬 플레이어 종족 정적 홀더 (Set/Current/Reset)
- `Infrastructure/GameRaceContext.cs` — BlueRace/RedRace 정적 홀더 (멀티플레이 수신용)
- `Presentation/UI/ViewModels/RaceSelectionViewModel.cs` — UniRx ReactiveProperty, CmdPrev/CmdNext, LocalPlayerRace.Set 연동
- `Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs` — 캐러셀 DOTween, Animator CrossFade 1초, IView 패턴
- `Presentation/UI/Views/Lobby/Battle/BattleMainView.cs` — BindRace() 메서드 추가, RaceSelectionView 항상 표시(독립 토글 제거)
- `Editor/RaceSelectionPreviewSetup.cs` — 씬 자동 구성 에디터 스크립트 (CharacterPreview 레이어, RT 512×512, 카메라 Z=-2, FOV=45)
- `Animations/Units/Pistoleer/Pistoleer.controller` — Idle 상태 m_Speed 0→1 수정

**핵심 설계**:
- RaceSelectionView는 BattlePanel(BattleRootView) 직속 자식, anchorMin=(0,0) anchorMax=(1,0.5) — BattleMainPanel과 sibling
- BattleMainPanel: 상단 50% (anchorMin.y=0.5, anchorMax.y=1.0)
- RaceSelectionView 항상 표시 — BattleMainPanel(버튼 영역)만 CurrentScreen에 따라 토글
- RaceSelectionViewModel은 BattleRootView에서 생성/Dispose, BattleMainView.BindRace()로 전달
- CharacterPreview 레이어 격리 → RenderTexture → RawImage(CharacterDisplay)
- AnimBlendTime = 1.0f (_moveDuration과 동일), offset 0(중앙)=Walk, offset 1,2(좌우)=Idle

**캐러셀 위치 (씬 확정값)**:
- CenterPos: (1000, 0.35, 2), LeftPos: (999.7, 0.1, 5), RightPos: (1000.3, 0.1, 5)
- 카메라: (1000, 1.5, -2), Rotation: Euler(12, 0, 0), FOV=10

**Pistoleer Idle 버그 교훈**:
- Animator Controller 상태의 m_Speed 값 직접 확인 필수 (Editor에서 설정하지 않으면 0이 될 수 있음)
- m_Speed: 0이면 애니메이션이 첫 프레임에서 동결됨

**Android URP RenderTexture 잔상 버그 교훈 (2026-04-06)**:
- 근본 원인: RT 에셋 파일(`m_AntiAliasing: 2`)과 카메라 설정(`allowMSAA=false`, 1 sample) 간 sample count 불일치
- 에러: `Attachment 0 was created with 1 samples but 2 samples were requested`
- 현상: sample count 충돌 → URP Render Pass clear 실패 → 이전 프레임 타일 메모리 로드 → 잔상
- 수정 체크리스트 (RenderTexture 전용 카메라 설정):
  - RT 에셋: `m_AntiAliasing: 1` (YAML 직접 확인 필수 — EnsureRenderTexture 코드 수정만으로 반영 안 될 수 있음)
  - Camera: `allowMSAA = false`, `allowHDR = false` (기본값 true라 명시적으로 꺼야 함)
  - Camera: `backgroundColor.alpha = 1` (alpha=0이면 일부 Android GPU 드라이버 clear 생략)
  - URP: `urpData.antialiasing = AntialiasingMode.None`
  - URP: `urpData.renderType = CameraRenderType.Base`
  - URP: `urpData.renderShadows = false`

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-04/21_00_race-selection-ui/`

### 재경기 초기화 버그 수정 (2026-04-04) ✅ 테스트 완료

**증상**: 재경기 시 이전 게임 유닛/건물이 씬에 잔존
**원인**: NGO SceneManager.LoadScene(Single)으로 같은 씬 재로드 시 동적 스폰 NetworkObject 자동 Despawn 미보장
**수정**: `NetworkGameEndController.StartRematch()`에서 LoadScene() 직전 SpawnManager.SpawnedObjects 순회 → 동적 NetworkObject 명시적 Despawn

**핵심 패턴**:
- `SpawnedObjects.Values`를 `List<NetworkObject>` 복사본으로 순회 (Despawn 중 컬렉션 변경 방지)
- `IsSceneObject == false`만 Despawn (씬 배치 오브젝트 자동 제외)
- `IsSpawned == true` / `IsSceneObject == false` — NGO 2.9.x에서 bool? (nullable) 비교 방식 필수

**교훈**:
- `DestroyWithScene = true`는 같은 씬 재로드 시나리오에서 동작 불보장
- 같은 씬 재로드 전에는 반드시 동적 NetworkObject를 명시적으로 Despawn해야 함
- `Active Scene Synchronization`은 씬 전환용 설정 — 같은 씬 재로드와 무관

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/20_00_rematch-initialization-bug/`

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
- Root Motion 반드시 OFF
- **Animator Controller 상태 m_Speed 주의**: 기본값 0이면 애니메이션 첫 프레임 동결. 새 상태 추가 시 m_Speed=1 확인 필수
