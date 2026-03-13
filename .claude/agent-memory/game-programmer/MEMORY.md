# Game Programmer Agent Memory

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 attack direction 작업 전체 삭제 (복구 불가)
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## ⚠️ 구현 시 필수 확인 제약 (컴파일 에러 예방 — 매 작업 시작 전 확인)

### 레이어 제약
- Domain 레이어: `using Hexiege.Core` 절대 금지 → HexOrientationContext 등 정적 홀더 패턴 사용
- NetworkBehaviour: Infrastructure 레이어에만 배치 (Presentation/Application 금지)
- Application 레이어: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 패턴 사용
- GameBootstrapper = 유일한 의존성 조합 루트 → 새 UseCase/Controller 추가 시 반드시 여기서 와이어링
- 새 파일 추가 시 반드시 레이어별 네임스페이스 확인 (Assembly Definition 없음 — 네임스페이스 규약만)

### NGO API 제약
- ServerRpc 메서드명: 반드시 `ServerRpc` 로 끝나야 함
- ClientRpc 메서드명: 반드시 `ClientRpc` 로 끝나야 함
- NGO 2.9.2, Enable Scene Management = ON 필수
- NetworkBehaviour 는 씬에 NetworkObject로 배치해야 RPC 작동 (별도 Spawn 코드 불필요)
- RPC 파라미터: 직렬화 가능 타입만 허용 (INetworkSerializable 또는 기본 타입/enum)
- 클라이언트 전용 로직 분기: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer` 패턴 사용

## 전투 버그 수정 — 자세한 내용: [combat-fixes.md](combat-fixes.md)
- UnitCombatUseCase: `ClaimedTile ?? Position` 으로 사거리/방향 계산 (Lerp 중 위치 보정)
- UnitView: 부드러운 회전 (ApplyDirection → _targetYRotation, Update에서 MoveTowardsAngle 보간, 540도/초)

## IEntityPositionProvider — 월드좌표 기반 사거리 판정 (2026-03-07 재구현)
- **문제**: HexCoord.Distance는 Lerp 완료 후에만 갱신 → 이동 중 최대 0.8초 공격 딜레이
- **해결**: UnitFactory/BuildingFactory.GetObject()로 실시간 Transform.position 조회
- **신규 파일**:
  - `Application/Interfaces/IEntityPositionProvider.cs` — GetUnitWorldPosition(id), GetBuildingWorldPosition(id)
  - `Infrastructure/UnitWorldPositionProvider.cs` — UnitFactory+BuildingFactory 주입, GetObject().transform.position 반환
- **수정 파일**:
  - `UnitCombatUseCase.cs`: 생성자에 `IEntityPositionProvider positionProvider=null` 추가, FindFirstEnemyTarget→월드좌표 Vector3.Distance 판정, null/zero시 HexCoord 폴백
  - `GameBootstrapper.cs`: CreateUseCases()에서 `new UnitWorldPositionProvider(_unitFactory, _buildingFactory)` 생성 후 전달
- **임계값**: `attacker.AttackRange * HexMetrics.TileHeight` (epsilon 없음 — 2026-03-14 수정. 타일 중심 간 정확한 거리 기준. +0.1f 제거 이유: Lerp 완료 전 조기 공격 발동 → ProcessStep 미호출 → 타일 점령 안 됨)

## 네트워크 인프라 (Phase 1)
- 패키지: `com.unity.netcode.gameobjects` 2.8.1, `com.unity.services.multiplayer` 2.0.0 (Lobby/Relay/Auth 통합) 이미 설치됨
- 파일 위치: `Assets/_Project/Scripts/Infrastructure/Network/`
  - `UnityServicesInitializer.cs` — UGS 초기화 + 익명 로그인 (순수 C# 클래스)
  - `LobbyManager.cs` — Lobby CRUD + Heartbeat 코루틴 (순수 C# 클래스)
  - `RelayManager.cs` — Relay 할당/참가 + UnityTransport 설정 (순수 C# 클래스)
  - `NetworkGameManager.cs` — 전체 세션 흐름 관리 (MonoBehaviour, DontDestroyOnLoad)

## 핵심 API 매핑
- LobbyService.Instance: `Unity.Services.Lobbies.LobbyService`
- RelayService.Instance: `Unity.Services.Relay.RelayService`
- AuthenticationService.Instance: `Unity.Services.Authentication.AuthenticationService`
- UnityServices.InitializeAsync(): `Unity.Services.Core.UnityServices`
- Allocation → RelayServerData: `allocation.ToRelayServerData("dtls")` (AllocationUtils 확장 메서드, `Unity.Services.Relay.Models` 네임스페이스)
- UnityTransport.SetRelayServerData(): `Unity.Netcode.Transports.UTP.UnityTransport`
- Relay 연결 프로토콜: 모바일 = "dtls", WebGL = "wss"

## Lobby 데이터 컨벤션
- Relay Join Code 키: `LobbyManager.RelayJoinCodeKey = "RelayJoinCode"`
- DataObject.VisibilityOptions.Public 으로 저장해야 Client 가 읽을 수 있음

## NetworkGameManager 흐름
- Host: InitializeAsync → HostGameAsync(lobbyName) → [Relay 생성 → Lobby 생성 → StartHost()]
- Client: InitializeAsync → JoinGameAsync(lobbyCode) → [Lobby 참가 → RelayJoinCode 추출 → JoinRelay → StartClient()]
- 에디터 수동 작업: NetworkManager GameObject 씬 배치 + UnityTransport 컴포넌트 추가 필요

## 네트워크 인프라 (Phase 2) — 팀 할당 + 게임 시작 흐름
- `LocalPlayerTeam.cs` — 정적 팀 홀더 (싱글플레이 기본값 Blue, 네트워크 시 갱신)
- `TeamAssigner.cs` — NetworkBehaviour, Player Prefab에 부착, Host=Blue/Client=Red 자동 할당
  - NetworkVariable<int> _assignedTeamIndex (Server Write Only)
  - UniRx Subject<TeamId> OnTeamAssigned 이벤트
- `NetworkGameFlow.cs` — NetworkBehaviour, 씬에 NetworkObject로 배치
  - 모든 플레이어 준비 신호 수집 (RequestReadyServerRpc) → StartGameClientRpc
  - GameBootstrapper.StartNetworkGame(TeamId) 호출
- `GameBootstrapper.StartNetworkGame(TeamId)` — 네트워크 게임 전용 진입점
  - LoadMap() 후 팀에 따른 카메라 시작 위치 설정

## 팀 매핑 (TeamId)
- TeamId.Neutral = 0, TeamId.Blue = 1, TeamId.Red = 2
- 네트워크: Host(OwnerClientId=0) → Blue, Client → Red
- TeamAssigner._assignedTeamIndex 내부 인덱스: 0=Blue, 1=Red (TeamId와 다름!)
- NetworkBuildingController에서는 TeamId 정수값 직접 전송 (Blue=1, Red=2)

## GameBootstrapper Start() 분기 패턴
- NetworkManager.Singleton이 null이거나 IsHost/IsClient가 false → 싱글플레이 (LoadMap 즉시 실행)
- 네트워크 모드 → 맵 로드 건너뜀, NetworkGameFlow가 StartNetworkGame() 호출 대기
- C# 버전: LangVersion 9.0 (switch expression 사용 가능)

## 네트워크 인프라 (Phase 3) — 타일/자원 동기화
- `TileOwnershipData.cs` — INetworkSerializable 구조체 (Q, R, TeamIndex)
- `NetworkTileSync.cs` — NetworkBehaviour, 씬에 NetworkObject 배치
  - 서버: OnTileOwnerChanged 구독 → BroadcastTileChangeClientRpc(q, r, teamIndex)
  - 클라이언트: grid.SetOwner() + GameEvents 재발행 → HexTileView 색상 자동 갱신
- `NetworkResourceSync.cs` — NetworkBehaviour, 씬에 NetworkObject 배치
  - NetworkVariable<int> _blueGold / _redGold (Server Write Only)
  - 서버: OnResourceChanged 구독 → NetworkVariable 갱신 (NGO 자동 전파)
  - 클라이언트: OnValueChanged → ApplyGoldToLocalUseCase() → AddGold(diff) → HUD 갱신
- `GameBootstrapper` — GetGrid() / GetResource() public 메서드 추가
- `NetworkGameFlow.StartGameClientRpc()` — 맵 로드 후 서버가 초기 골드 강제 발행 (SyncInitialGold)

## 네트워크 인프라 (Phase 4) — 건물 배치 동기화
- `NetworkBuildingController.cs` — NetworkBehaviour, 씬에 NetworkObject 배치
  - 클라이언트 UI → RequestBuildServerRpc(buildingTypeInt, teamIndex, q, r)
  - 서버: ClientId→TeamId 매핑 검증 + 골드 확인 + PlaceBuilding() → SpawnBuildingClientRpc
  - 클라이언트: PlaceBuildingWithId(id, ...) → OnBuildingPlaced 발행 → BuildingFactory 프리팹 생성
  - 실패 시: BuildFailedClientRpc로 요청자에게만 피드백 전송
- `BuildingData` — ID 지정 생성자 오버로드 추가 (int id 선두 파라미터)
  - _nextId를 지정 Id+1로 갱신하여 이후 자동 발급 ID 충돌 방지
- `BuildingPlacementUseCase` — PlaceBuildingWithId(id, type, team, coord) 추가
  - 클라이언트 측 도메인 재생성 전용 (검증 생략, 이벤트 발행 포함)
- `BuildingPlacementUI` — Initialize에 NetworkBuildingController 파라미터 추가 (기본값 null)
  - PlaceAndClose: 멀티플레이 시 RequestBuildServerRpc 호출, 싱글플레이 시 기존 흐름 유지
- `GameBootstrapper` — GetBuildingPlacement() / GetConfig() 공개 메서드 추가
  - [SerializeField] _networkBuildingController 추가
  - SetupBuildings()에서 네트워크 모드 확인 후 UI에 컨트롤러 주입

## 네트워크 인프라 (Phase 5) — 유닛 생산 동기화
- `NetworkProductionController.cs` — NetworkBehaviour, 씬에 NetworkObject 배치
  - 클라이언트 UI → RequestEnqueueServerRpc(barracksId, unitTypeInt, teamIndex)
  - 서버: 팀 소유권·골드·인구·배럭 존재 검증 + EnqueueUnit() 실행 (골드 즉시 차감)
  - 서버: OnUnitProduced 구독 → SpawnUnitClientRpc(unitId, type, team, q, r, rallyQ, rallyR, hasRally)
  - 서버: OnProductionStarted 구독 → ProductionStartedClientRpc(barracksId, type, requiredTime)
  - 서버: OnProductionQueueChanged 구독 → SyncQueueStateClientRpc(barracksId, current, q0, q1, isAuto, progress)
  - 클라이언트: SpawnUnitWithId(id, ...) → OnUnitSpawned + OnUnitProduced 발행 → UnitFactory 프리팹 + ProductionTicker 랠리 이동
  - 클라이언트: ProductionStartedClientRpc → ProductionState에 타이머 설정 → 프로그레스 바 시뮬레이션
  - 클라이언트: SyncQueueStateClientRpc → ManualQueue + CurrentProducing 스냅샷 동기화
  - 실패 시: EnqueueFailedClientRpc로 요청자에게만 피드백
- `UnitData` — ID 지정 생성자 오버로드 추가 (int id 선두 파라미터, BuildingData와 동일 패턴)
- `UnitSpawnUseCase` — SpawnUnitWithId(id, type, team, coord) 추가
  - 클라이언트 측 재생성 전용: IsWalkable/중복 검증 생략, 이벤트 발행 포함
- `ProductionTicker.Update()` — 서버/클라이언트 분기
  - 서버: Tick(생산 로직) + TickIncome + TickSiege 실행
  - 클라이언트: TickProgressOnly(프로그레스 바 시각용) + TickSiege만 실행
- `UnitProductionUseCase.TickProgressOnly(dt)` — 클라이언트 전용: ElapsedTime만 진행, 생산 로직 없음
- `ProductionPanelUI` — Initialize에 NetworkProductionController 파라미터 추가 (기본값 null)
  - OnPistoleerTap: 멀티플레이 시 RequestEnqueueServerRpc 호출, 싱글플레이 시 기존 흐름
  - OnPistoleerLongPress: 멀티플레이에서는 자동 생산 미지원 (로그 경고 후 return)
- `GameBootstrapper` — GetUnitProduction() / GetUnitSpawn() / GetPopulation() 공개 메서드 추가
  - [SerializeField] _networkProductionController 추가
  - SetupProduction()에서 네트워크 모드 확인 후 UI에 컨트롤러 주입

## 동기화 타이밍 주의사항
- NetworkTileSync/ResourceSync 스폰 시점에 HexGrid/ResourceUseCase가 null일 수 있음 (맵 로드 전)
  → BroadcastTileChangeClientRpc / ApplyGoldToLocalUseCase에서 null 방어 처리
- ResourceUseCase 생성자는 OnResourceChanged를 발행하지 않음
  → NetworkGameFlow.SyncInitialGold()에서 맵 로드 후 초기 골드 강제 발행
- AddGold(team, negativeAmount) = 골드 감소 (내부에서 _gold[team] += amount)

## 네트워크 인프라 (Phase 6) — 유닛 이동 + 전투 네트워킹
- `NetworkUnitMovementController.cs` — NetworkBehaviour, 씬에 NetworkObject 배치
  - 공개 메서드 RequestMove(unit, target, unitFactory, movementUseCase): 클라이언트 예측 이동
  - 클라이언트: 로컬 즉시 이동(UnitView.MoveTo) + RequestMoveServerRpc 전송
  - 서버: 팀 소유권 검증 + 경로 계산 + 서버 UnitView 이동 + SyncMovementClientRpc(요청자 제외)
  - 클라이언트(상대방): SyncMovementClientRpc 수신 → UnitFactory.GetUnitObject() → UnitView.MoveTo()
- `NetworkCombatController.cs` — NetworkBehaviour, 씬에 NetworkObject 배치
  - OnNetworkSpawn: NetworkContext.Set(IsServer, isActive=true) 호출 (Application 레이어 분기용)
  - OnNetworkDespawn: NetworkContext.Reset() 호출
  - 서버 Update: 유닛별 개별 쿨다운(AttackDuration) 기반 전투 처리 (_unitAttackTimers Dictionary)
  - 쿨다운 = UnitData.AttackDuration = 공격 애니메이션 클립 길이 (Pistoleer=2.0초)
  - 이전 방식(_attackInterval=0.2f 전역 타이머) 제거 → 애니메이션 반복 재시작 버그 해결
  - 서버: OnEntityDied 구독 → EntityDiedClientRpc(entityId, isUnit) 전파
  - 클라이언트: HandleUnitDied / HandleBuildingDied → TakeDamage(HP 소진) → RemoveUnit/Building → OnEntityDied 재발행
- `NetworkHealthSync.cs` — NetworkBehaviour, 씬에 NetworkObject 배치 (Phase 6에 구현 완료)
  - 서버: OnEntityDamaged 구독 → SyncHealthClientRpc(entityId, isUnit, serverHp)
  - 클라이언트: 현재 HP와 서버 HP 차이만큼 TakeDamage로 맞춤

## AI 이동(Siege/랠리) 서버 권한 동기화 패턴
- **문제**: ProductionTicker의 Siege/랠리 이동이 서버·클라이언트 양쪽 독립 실행 → 각자 다른 경로 → 화면 불일치
- **해결**: 서버만 이동 경로 결정, 클라이언트는 BroadcastMoveClientRpc로 수신
- `NetworkUnitMovementController`: BroadcastServerMove(unitId, path) + BroadcastMoveClientRpc (AI 이동 전용, 모든 클라이언트 전파)
  - 기존 RequestMove/SyncMovement (플레이어 수동 이동)와 별도 — 양쪽 공존
- `ProductionTicker`: _networkMovement 필드 추가 (Initialize에서 주입, 싱글플레이 시 null)
  - IsNetworkClient 프로퍼티: 멀티플레이 클라이언트 판별
  - BroadcastMoveIfServer(unitId, path): 서버일 때만 _networkMovement.BroadcastServerMove() 호출
  - OnUnitProduced: 클라이언트이면 이동 명령 전체 건너뜀 (return)
  - MoveTowardEnemyCastle: MoveTo 후 BroadcastMoveIfServer 추가
  - TickSiege: 클라이언트이면 상태 정리만 수행(dead unit 제거 등), 이동 명령 스킵
- `GameBootstrapper.SetupProduction()`: isNetworkMode 시 _networkUnitMovement를 ProductionTicker에 주입

## NetworkContext 패턴 (Application 레이어용 네트워크 상태 홀더)
- 파일: `Assets/_Project/Scripts/Application/NetworkContext.cs`
- 목적: Application 레이어가 Unity.Netcode(NetworkManager)에 직접 의존하는 것을 방지
- 사용 패턴: NetworkCombatController.OnNetworkSpawn() → NetworkContext.Set(IsServer, true)
- UnitCombatUseCase.TryAttack(): `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;` (IDamageable 반환)
- HexOrientationContext, LocalPlayerTeam과 동일한 정적 홀더 패턴

## 네트워크 인프라 (Phase 7) — 승패 판정 동기화
- `NetworkGameEndController.cs` — NetworkBehaviour, 씬에 NetworkObject 배치
  - 서버: OnGameEnd 구독 → AnnounceWinnerClientRpc(winnerTeamIndex) 전파
  - 클라이언트: AnnounceWinnerClientRpc 수신 → OverrideRestartForMultiplayer → ShowResult(winner, localTeam)
  - _announced 플래그로 중복 전파 방지 (GameEndUseCase.IsGameOver와 이중 방어)
  - 멀티플레이 재시작: NetworkManager.Shutdown() → SceneManager.LoadScene(_lobbySceneName)
- `GameEndUseCase.cs` — 멀티플레이 클라이언트 분기 추가
  - `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer` → OnGameEnd 발행 생략
  - 싱글플레이/서버에서는 기존 OnGameEnd 발행 유지
- `GameEndUI.cs` — 멀티플레이 지원 메서드 2개 추가
  - ShowResult(winnerTeam, localTeam): 로컬 팀 기준 승/패 표시 (Red 팀 플레이어 대응)
  - OverrideRestartForMultiplayer(callback): 재시작 버튼을 멀티플레이 종료 흐름으로 교체
- `GameBootstrapper` — _networkGameEnd SerializeField + GetGameEndUI() 접근자 추가

## 승패 동기화 설계 원칙
- 싱글플레이: GameEndUseCase → OnGameEnd → GameEndUI.OnGameEnd (Blue 팀 고정) [기존 유지]
- 멀티플레이: GameEndUseCase(서버) → OnGameEnd → NetworkGameEndController → AnnounceWinnerClientRpc → GameEndUI.ShowResult(localTeam 기준)
- 클라이언트의 GameEndUseCase는 OnGameEnd를 발행하지 않음 → GameEndUI 중복 표시 방지
- GameEndUI.OnGameEnd는 싱글플레이 전용, 멀티플레이는 ShowResult를 경유

## GameBootstrapper 공개 접근자 전체 목록
- GetGrid(), GetResource(), GetBuildingPlacement(), GetConfig()
- GetUnitProduction(), GetUnitSpawn(), GetPopulation()
- GetMovement(), GetCombatUseCase(), GetUnitFactory(), GetGameEndUI()

## 클라이언트 전투 시각 동기화 패턴
- 문제: 클라이언트 UnitView Lerp에서 TryAttack()이 항상 false (NetworkContext 분기)
  → 적을 시각적으로 통과하는 버그
- 해결: UnitCombatUseCase.HasEnemyInRange() 추가 (네트워크 권한 체크 없음, 판정만)
  → 클라이언트 Lerp에서 HasEnemyInRange가 true이면 Idle 전환 + 대기
  → 서버 EntityDiedClientRpc로 적 제거 시 HasEnemyInRange가 false → Lerp 재개
- UnitView.StopMovement() public 메서드 추가 (외부 이동 중단용)

## Walk 애니메이션 연속 재생 (2026-03-09 수정)
- **문제**: MoveAlongPath 매 스텝 시작 시 `_animator.Play(StateWalk, 0, 0f)` → normalizedTime=0f 리셋 → 클립이 끝까지 재생 안 되고 반복
- **수정**: Walk 상태 여부 체크 후 조건부 Play
  ```csharp
  if (!_animator.GetCurrentAnimatorStateInfo(0).shortNameHash.Equals(StateWalk))
      _animator.Play(StateWalk, 0, 0f);
  _animator.speed = 1f;
  ```
- **효과**: 이미 Walk 재생 중이면 클립 유지 → 자연스러운 연속 걷기 애니메이션

## 유닛 확정 스탯 (2026-03-14 최종 확정)
| 항목 | Pistoleer | Assault | Sniper |
|------|-----------|---------|--------|
| HP | 30 | 50 | 30 |
| AttackPower | 3 | 6 | 20 |
| AttackRange (float) | 1.0 | 2.0 | 5.0 |
| MoveSeconds | 1.0 | 1.0 | 4.0 |
| ProductionTime | 5s | 10s | 15s |
| GoldCost | 50 | 100 | 200 |
| AttackCooldown | 1.0 (클립 길이 덮어씀) | 1.0 | 1.0 |

- **AttackRange 타입 변경**: `UnitData.AttackRange` int → float, `UnitStats.GetAttackRange` int → float
  - 영향 파일: `UnitData.cs`, `UnitStats.cs`, `UnitSpawnUseCase.cs`(생성자 파라미터)
  - 주 경로(IEntityPositionProvider): `attacker.AttackRange * HexMetrics.TileHeight` → float 자동 호환
  - 폴백 경로(HexCoord): `distance <= attacker.AttackRange(float)` C# 암시적 변환으로 컴파일 OK

## 유닛별 개별 이동속도
- UnitData.MoveSeconds (float, readonly) — 타일 1칸 이동 소요 시간
- UnitStats.GetMoveSeconds(UnitType) — 타입별 기본값 (Pistoleer=1.0, Assault=1.0, Sniper=0.5)
- UnitView.MoveAlongPath: _unitData.MoveSeconds 참조 (GameConfig.UnitMoveSeconds 대신)

## 중요 교훈
- `com.unity.services.multiplayer` 2.0.0 은 Lobby + Relay + Auth 를 모두 포함하는 통합 패키지
- NetworkBehaviour 는 Infrastructure 레이어에만 (Presentation이 아님!)
- NetworkGameManager 는 Infrastructure 에 MonoBehaviour 로
- LobbyService.Instance 사용 전 UnityServices.InitializeAsync() 완료 필요 (미완료 시 InvalidOperationException)
- Heartbeat 코루틴은 MonoBehaviour(NetworkGameManager) 에서 StartCoroutine 으로 실행
- NetworkGameFlow는 NetworkObject로 씬에 배치해야 ServerRpc/ClientRpc가 작동함
- TeamAssigner는 Player Prefab에 부착 (NetworkManager의 PlayerPrefab 필드에 등록)
- 씬 배치 NetworkObject는 Host StartHost() 시 자동 스폰됨 (별도 Spawn 코드 불필요)
- Domain 레이어 최소 수정 원칙: ID 지정 생성자 오버로드처럼 기존 생성자를 건드리지 않고 추가
- 서버의 PlaceBuilding() 실행 시 이미 GameEvents가 발행되어 서버 측 BuildingFactory가 프리팹 생성
  → SpawnBuildingClientRpc에서 IsServer 체크로 서버 중복 처리 방지

## 네트워크 인프라 (Phase 8) — UI/UX 네트워크 대응
- `GameHudUI.cs` — _isNetworkMode 캐시 + LocalPlayerTeam.Current 로 적팀 골드 표시 추가
  - [SerializeField] _enemyInfoPanel (GameObject), _enemyGoldText (TMP) 추가
  - Initialize()에서 네트워크 모드 판단 → enemyInfoPanel.SetActive(_isNetworkMode)
  - 싱글플레이: localTeam = Blue 고정, 적팀 패널 비활성
- `NetworkStatusUI.cs` — Presentation 레이어 (MonoBehaviour, NetworkBehaviour 불필요)
  - UnityTransport.GetCurrentRtt(ServerClientId) 사용 (ulong 반환)
  - namespace: Unity.Netcode.Transports.UTP (UnityTransport 캐스팅 필요)
  - 서버는 OnClientDisconnect 시 ReconnectionHandler에 위임, 팝업 미표시
  - 클라이언트는 서버 끊김 감지 시 팝업 표시 → SceneManager.LoadScene 복귀
- `ReconnectionHandler.cs` — Infrastructure/Network/ (NetworkBehaviour)
  - IsServer 확인 후 OnClientDisconnectCallback 등록 (클라이언트는 enabled=false)
  - _reconnectWaitSeconds(기본 30초) 대기 후 NetworkGameEndController.ForceWin() 호출
  - 재접속 시 StopCoroutine으로 코루틴 취소
  - LocalPlayerTeam.Current로 서버(Host) 팀 확인 → 남은 팀(서버 팀) 승리 처리
- `NetworkGameEndController.cs` — ForceWin(int winnerTeamIndex) public 메서드 추가
  - IsServer + _announced 체크 후 AnnounceWinnerClientRpc 호출 (기존 경로 재사용)
- `LobbyUI.cs` — Presentation 레이어 (MonoBehaviour)
  - NetworkGameManager.HostGameAsync / JoinGameAsync async 호출
  - _isWorking 플래그로 중복 입력 방지
  - OnClientConnectedCallback: 2명 연결 시 LobbyPanel 숨김
  - NetworkGameManager.OnHostStarted 이벤트로 Join Code 표시
  - Start()에서 InitializeAsync() 자동 호출 (UGS 초기화)
- `GameBootstrapper.cs` — _reconnectionHandler SerializeField 추가 (Inspector 와이어링용)

## RTT API 요점
- `UnityTransport.GetCurrentRtt(ulong clientId)` — ulong 반환 (ms 단위)
- 네임스페이스: `Unity.Netcode.Transports.UTP`
- NetworkManager.NetworkConfig.NetworkTransport as UnityTransport 캐스팅 필요
- Host에서 서버 RTT = 0에 가까움 (로컬 루프백)
- ServerClientId 상수 = NetworkManager.ServerClientId

## ViewConverter (팀별 관점 변환 시스템)
- 파일: `Assets/_Project/Scripts/Core/ViewConverter.cs` (정적 클래스, Core 레이어)
- 목적: Red팀 클라이언트에서 맵을 반전하여 자기 진영이 화면 하단에 보이도록 함
- 공식: `viewPos = 2 * mapCenter - domainPos` (자기 역함수: FromView = ToView)
- 방향 반전: `FlipDirection(dir) = (dir + 3) % 6` (Red팀만)
- 카메라 Z축 회전 방식은 사용하지 않음 (스프라이트가 뒤집힘)
- CameraController.SetTeamView() 삭제됨 → ViewConverter로 대체
- [수정됨] 올바른 초기화 순서:
  1. StartNetworkGame() → ViewConverter.Setup(isRed, mapCenter) 먼저 호출
  2. 그 다음 LoadMap() 호출 → 타일/건물/금광 렌더링이 올바른 반전 위치에 적용됨
  - 이전 방식(LoadMap 후 Setup + 타일 재렌더링)은 건물이 반전 안 되는 버그 있었음
- [수정됨] LoadMap() 내 ViewConverter.Reset() 분기:
  - 싱글플레이(isNetworkMode=false): ViewConverter.Reset() 실행 (기존 동작 유지)
  - 네트워크 모드(isNetworkMode=true): ViewConverter.Reset() 건너뜀 (Setup 상태 유지)
- 적용 위치: HexGridRenderer, UnitFactory, BuildingFactory, UnitView, InputHandler, ProductionTicker
- 도메인 좌표는 항상 Blue 기준 유지 — 뷰 레이어에서만 반전

## 건물 렌더링 버그 수정 이력

### [1차 수정] sortingOrder 버그 (이전 수정 — 원인 오분석)
- 증상: Castle/MiningPost 스프라이트가 일부 타일 아래에 가려짐
- 수정: BuildingFactory에서 sortingOrder 동적 계산 추가
  - FlatTop: `ViewConverter.FlatTopSortingOrder(viewPos) + 50`
  - PointyTop: `data.Position.R + 50`
- 수정 파일: `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`

### [2차 수정] transform.position 버그 — Red팀 건물 위치 틀어짐 (2026-02-22)
- 증상: Red팀에서 Castle/MiningPost GameObject의 transform.position이 실제 배치 타일보다 한 칸 이상 오프셋
  - Blue팀은 정상 (IsFlipped=false → ToView = 원래 좌표 그대로)
  - Red팀에서만 발생 (IsFlipped=true → ToView에서 잘못된 mapCenter 사용)
- 근본 원인: `StartNetworkGame()`에서 `HexMetrics.GridCenter()`를 `HexMetrics.Orientation = FlatTop`
  설정 이전에 호출. `HexMetrics.Orientation`의 기본값 = PointyTop이므로
  `GridCenter()`가 PointyTop 공식으로 mapCenter를 계산 → 실제 FlatTop 중심과 다른 값 반환
  → `ToView = 2*wrongMapCenter - pos`에서 위치 오프셋 발생
- 수정: `StartNetworkGame()`의 `GridCenter()` 호출 전에 `HexMetrics.Orientation`,
  `HexOrientationContext.Current`, `HexMetrics.TileWidth`, `HexMetrics.TileHeight`를
  FlatTop 기준으로 사전 설정. (이후 `LoadMap()`→`ApplyConfig()`에서 재설정되므로 중복이지만 무해)
- 수정 파일: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
  - 수정 메서드: `StartNetworkGame()`
- 교훈: ViewConverter.Setup()에 전달하는 mapCenter는 반드시 실제 사용할 Orientation으로
  HexMetrics를 사전 설정한 후 GridCenter()를 호출해야 정확한 값을 얻을 수 있음
### [3차 수정] Y 오프셋 적용 순서 버그 — Red팀 건물이 아래로 내려감 (2026-02-22)
- 증상: Red팀에서 건물이 타일보다 아래로 내려감 (Blue팀은 정상)
- 원인: `_buildingYOffset`을 `ViewConverter.ToView()` 이전에 적용 → Y축 반전 시 오프셋 방향도 반전
- 수정: `viewPos = ViewConverter.ToView(worldPos)` 이후에 `viewPos.y += _buildingYOffset` 적용
- 교훈: ViewConverter.ToView() 이후에 적용해야 하는 시각적 오프셋은 반드시 ToView 호출 뒤에 가산
- 동일 패턴 수정: UnitFactory.cs(HexToWorldUnit→HexToWorld+ToView후오프셋), UnitView.cs(MoveAlongPath의 from/toPos)

- [삭제됨] sortingOrder 계층 — Phase 2에서 3D Z-buffer로 대체

## HexTileView 팀 색상 시스템 (2026-03-01 수정 완료)
- 파일: `Assets/_Project/Scripts/Presentation/Grid/HexTileView.cs`
- **핵심 수정 1**: `material.color = X` → `material.SetColor("_BaseColor", X)` 로 변경
  - `material.color`는 `_Color` 프로퍼티를 변경 → 커스텀 Shader Graph에서 동작 안 함
  - SG_HexTile은 Blackboard에 `_BaseColor` (Reference: `_BaseColor`) 사용 → SetColor 필요
- **핵심 수정 2**: 재질 탐색을 셰이더 이름 기반으로 변경
  - ProBuilder 타일: materials[0]=mat_tile_side(Lit), materials[1]=mat_tile_top(SG_HexTile)
  - `renderer.material`(인덱스 0)은 side를 반환 → top 색상 변화 없음
  - Initialize()에서 `shader.name.Contains("SG_HexTile")` 루프로 정확한 재질 인스턴스 캐시
- **주의**: 새 3D 타일 프리팹(ProBuilder)에 `HexTileView` 컴포넌트 수동 추가 필요
  - ProBuilder는 MeshRenderer/MeshFilter만 자동 생성, HexTileView는 직접 Add Component

## 헥스 타일 (3D ProBuilder + Shader Graph)

### ProBuilder 타일 생성
- Shape: Cylinder, Sides=6, Height Cuts=0, Smooth=true
- Size: X=1.0, Y=0.1, Z=1.0
- 두 개의 Submesh 분리 필요: ProBuilder Face 모드에서 각 face에 Material Preset 적용해야 실제 Submesh가 생성됨
  - MeshRenderer Materials 배열에만 추가하면 런타임에 1개만 적용됨 (Submesh가 1개이면)
- 프리팹 경로: `Assets/_Project/Prefabs/Tiles/`
- mat_tile_top: SG_HexTile 셰이더, 밝은색 #BCBCBC, 테두리 #3A3A3A, 두께 0.02
- mat_tile_side: #3A3A3A 단색

### Shader Graph (SG_HexTile) — 타일 상단 테두리 효과
- 파일: `Assets/_Project/Materials/SG_HexTile` (URP Lit Shader Graph)
- **UV 기반 SDF 불가** — ProBuilder Cylinder cap의 UV 매핑이 예상과 달라 잘못된 패턴 생성
- **Object Space Position 기반 SDF 사용** (신뢰할 수 있는 방식)
- Custom Function 노드 (HexBorder):
  - Input: Position (Vector3), BorderSize (Float)
  - Output: Border (Float)
  - HLSL Body:
    ```hlsl
    float2 p = abs(float2(Position.x, Position.z));
    float d = max(p.y, p.x * 0.866 + p.y * 0.5);
    Border = step(0.433 - BorderSize, d);
    ```
  - d_max (FlatTop hex boundary, circumradius=0.5) = 0.433
  - BorderSize = 0.02 (실제 적용값)
- 노드 연결: Position(Object) → Custom Function → Lerp(T) / Color(밝은) → Lerp(A) / Color(어두운) → Lerp(B) / Lerp → Base Color
- ProBuilder Face 모드 진입: Scene View 왼쪽 상단 ≡(오버레이 메뉴) → ProBuilder 활성화 → ■(Face) 버튼

## 3D 전환 — 상세 내용은 [3d-transition.md](3d-transition.md) 참조

## XZ 좌표계 전환 (Phase 1 완료, 2026-02-27)
- 모든 헥스 좌표가 XZ 평면(Y=0)에 배치됨 (이전: XY 평면, Z=0)
- HexMetrics.HexToWorld(): `new Vector3(x, 0f, z)` 반환
- HexMetrics.WorldToHex(): X, Z 좌표 기반으로 역산
- ViewConverter.ToView(): X, Z 반전 (Y는 높이로 통과)
- CameraController: XZ 평면 레이캐스트 기반 팬
- InputHandler: ScreenToXZPlane() 헬퍼로 XZ 평면 레이캐스트

## 렌더링 전환 (Phase 2 완료, 2026-02-27)
- SpriteRenderer → Renderer/MeshRenderer 기반
- FrameAnimator 삭제 → Animator(Mecanim) 기반
- sortingOrder 완전 제거 → 3D Z-buffer
- ViewConverter.FlatTopSortingOrder() 제거
- UnitAnimationData 의존성 체인 제거 (UnitFactory/UnitView/GameBootstrapper)
- UnitView: flipX → Y축 회전 (DirectionAngles: NE=30, E=90, SE=150, SW=210, W=270, NW=330)
- SetDependencies 시그니처: `(GameConfig, UnitMovementUseCase, UnitCombatUseCase)` — animData 제거

## UnitView 애니메이션 시스템 (2026-03-07 Animator.Play() 방식 확정)
- **Animator Controller 파라미터**: `IsDead`(bool) 1개만 사용 — IsWalking/Attack trigger 제거됨
- **스테이트**: Walk(기본/루프), Attack, Dead — 이름 반드시 정확히 일치 필요
- **트랜지션**: `Any State → Dead (IsDead=true)` 만 유지. 나머지 트랜지션 없음
- **스테이트 해시 상수**: `StateAttack`, `StateWalk` (Animator.StringToHash) — AnimIsWalking/AnimAttack 제거됨
- **Animator.Play() 직접 호출 방식** (트랜지션 우회):
  - Walk 시작: `_animator.Play(StateWalk, 0, 0f)` + `_animator.speed = 1f`
  - Walk 정지(Idle): `_animator.speed = 0f` (현재 프레임 고정, Walk 상태 유지)
  - 공격: `_animator.Play(StateAttack, 0, 0f)` → `yield return null` → `clipLen = GetCurrentAnimatorStateInfo(0).length` → `WaitForSeconds(clipLen)` → Walk 복귀 없음(전투 루프 탈출 시에만)
  - 사망: `_animator.speed = 1f` + `_animator.SetBool(AnimIsDead, true)` (인라인, SetAnimatorBool 래퍼 미사용)
- **clipLen 안전 폴백**: clipLen <= 0f 시 0.5f 사용
- **연속 공격 시 Walk 플래시 없음**: Play(Attack) 직접 호출 → 공격 완료 후 Walk 복귀 안 함 → 루프 탈출 시에만 speed=1f
- **SetAnimatorTrigger 래퍼 제거됨** — SetAnimatorBool은 IsDead 전용으로만 잔류(미사용 상태)
- **모든 유닛 공통 컨벤션**: Walk/Attack/Dead 스테이트 이름 통일 → UnitView 코드 변경 없이 유닛 추가 가능
- **Idle 애니메이션 없음**: 게임 특성상 Walk speed=0으로 정지 표현 (Idle 클립 불필요)

## 카메라 틸트 + UnitView Animator 확인 (Phase 3 완료, 2026-02-27)
- CameraController: `_tiltAngle=55f` SerializeField 추가, Start()→ApplyTilt(), TiltAngle 프로퍼티
- ScreenToXZPlane(): Plane.Raycast 기반이라 틸트 후에도 정확히 작동
- 팬 시 Y 고정: `new Vector3(diff.x, 0f, diff.z)` 패턴 유지
- GameBootstrapper: SetupCamera()/SetCameraStartPositionForTeam()에 틸트 Z 오프셋 보정 추가
  - `zOffset = cameraHeight / tan(tiltAngle)`, `pos.z -= zOffset`
- UnitView: Phase 2에서 Animator 연동 이미 완성 — 추가 수정 없음

## 카메라 경계(ClampPosition) 개선 (2026-03-07)
- **배경**: 줌 레벨 무관 전 타일 영역 접근 가능하게 하되 경계 벗어남 방지
- **halfW/halfH 동적 계산**: 줌 레벨(orthographicSize) 반영
  - `halfW = orthographicSize * aspect`
  - `halfH = orthographicSize / sin(tiltAngle)` (55도 틸트에서 수직 가시 범위)
- **look-at point 변환 패턴** (55도 틸트 핵심):
  - camera.position.z ≠ 지면 look-at Z — `zOffset = cameraHeight / tan(tiltAngle)` 차이 존재
  - 클램프는 look-at 좌표 기준: `lookAtZ = pos.z + zOffset`
  - 클램프 후 역변환: `pos.z = lookAtZ - zOffset`
  - X축은 변환 불필요 (tilt가 Z축 방향만 영향)
- **매 프레임 호출**: `Update()`에서 `ClampPosition()` 직접 호출 — 줌 변경/초기 위치에서도 즉시 보정
  - HandlePan 내부 중복 호출 제거 (Update에서 통합 처리)
- **효과**: 줌인 외곽 → 줌아웃 시 순간이동 현상 방지, 초기 카메라 위치 오버런 방지

## 공격 방향 실제 Transform 기반 (2026-03-07 최종 확정)
- **TryAttack 반환 타입**: `(int id, bool isUnit)?` 튜플 (공격 성공 시 targetId + 타겟 종류 반환)
- **UnitView.CalculateAttackAngle(Vector3 targetWorldPos)**: 타겟 실제 transform.position → Atan2 → _meshYOffset 보정
  - HexCoord 기반이 아닌 실제 transform → Lerp 이동 중에도 정확한 방향
- **UnitView.GetTargetWorldPos(int targetId, bool targetIsUnit)**: _unitFactory / _buildingFactory로 실제 GameObject 조회
  - fallback: 이미 파괴된 경우 transform.forward 방향 유지
- **UnitView._meshYOffset**: [SerializeField] float, 기본값 30f (Unit_Pistoleer_Mesh의 localEulerAngles.y 보정)
- **TriggerAttackAnimation(int targetId, bool targetIsUnit)**: targetId로 실제 transform 조회 후 CalculateAttackAngle 호출
- **UnitView.SetDependencies**: `UnitFactory unitFactory = null, BuildingFactory buildingFactory = null` 파라미터 추가
- **싱글플레이 이벤트 구독**: `TriggerAttackAnimation(e.Target.Id, e.Target is UnitData)` 호출
- **TriggerAttackAnimationClientRpc**: `(unitId, targetQ, targetR)` → `(unitId, targetId, targetIsUnit)` — 클라이언트 직접 조회
- **BuildingFactory.GetBuildingObject(int buildingId)**: 신규 추가 (UnitFactory.GetUnitObject 동일 패턴)
- **GameBootstrapper**: SetDependencies 호출에 `_unitFactory, _buildingFactory` 추가
- **이동 방향은 변경 없음**: MoveAlongPath의 ApplyDirection(dir) 호출은 기존 HexDirection 기반 유지

## 유닛별 AttackCooldown 시스템 (2026-03-06 구현)
- **UnitData.AttackCooldown** (float, get/set): 공격 쿨다운(초). UnitFactory에서 Attack 클립 길이로 덮어씀
- **UnitData.AttackCooldownRemaining** (float, get/set): 남은 쿨다운. 0이면 즉시 공격 가능
- **UnitStats.GetAttackCooldown(UnitType)**: 기본값 반환 (Pistoleer=1.0f) — UnitFactory가 클립 길이로 덮어씀
- **UnitFactory.GetAttackClipLength(Animator)**: runtimeAnimatorController.animationClips에서 "Attack" 포함 클립 길이 반환
- **NetworkCombatController**: `_attackInterval=0.1f` (폴링 빈도), 매 Tick `AttackCooldownRemaining -= _attackInterval`
- **UnitView.Update()**: 싱글플레이에서만 `AttackCooldownRemaining -= Time.deltaTime` (멀티플레이는 서버 Tick)
- **MoveAlongPath 이동 차단**: `HasEnemyInRange()` 기반 (쿨다운 무관한 적 존재 여부만 판정)

## 랠리포인트 마커 Transform Inspector 조정 (2026-03-07)
- **GameConfig.RallyMarkerOffset** (Vector3, default: 0.05/0.15/0): 마커 위치 오프셋 — Inspector에서 조정
- **GameConfig.RallyMarkerEuler** (Vector3, default: 0/0/0): 마커 회전 Euler 각도 — Inspector에서 조정
- **ProductionTicker.CreateOrMoveMarker()**: 하드코딩 `RallyMarkerOffset` 상수 제거 → `_config.RallyMarkerOffset/RallyMarkerEuler` 참조
- 기존 마커 이동 시에도 rotation 갱신 적용

## 팀별 피아식별 프리팹 시스템 (2026-03-14 에셋+코드 연동 완료)
- **에셋 위치**:
  - 유닛: `Assets/_Project/Prefabs/Units/Unit_{Type}_{Blue|Red}.prefab` (Pistoleer/Assault/Sniper × 2)
  - 건물: `Assets/_Project/Prefabs/Buildings/Building_{Type}_{Blue|Red}.prefab` (Castle/Barracks × 2)
  - 초상화: `Assets/_Project/Sprites/Units/{Type}/{type}_portrait_{blue|red}.png`
- **완료된 코드 연동**:
  - `UnitType.cs`: `Pistoleer=0`, `Assault=1`, `Sniper=2` ✅
  - `UnitFactory.cs`: `UnitTeamPrefabSet` struct (`_bluePrefabs`/`_redPrefabs`) — 팀+타입별 프리팹 선택 ✅
  - `BuildingFactory.cs`: `BuildingTeamPrefabSet` struct (`_bluePrefabs`/`_redPrefabs`) — 팀별 분기 ✅
  - `ProductionPanelUI.cs`: Assault/Sniper 버튼+초상화+생산 로직 완료 ✅
  - `UnitStats.cs`: 3종 유닛 스탯 정의 완료 ✅
  - `UnitProductionStats.cs`: 3종 유닛 생산시간/비용 정의 완료 ✅

## 팀별 초상화 동적 업데이트 (2026-03-14 완료)
- `ProductionPanelUI.cs`: `UpdateButtonPortraits(TeamId team)` — Show(barracks) 호출 시 팀 스프라이트 교체
  - `UnitPortraitSet` struct: `pistoleer`, `assault`, `sniper` 필드
  - `_bluePortraits` / `_redPortraits` Inspector 연결 필요
- `BuildingPlacementUI.cs`: `UpdateButtonPortraits(TeamId team)` — Show(coord, team) 호출 시 배럭 초상화 교체
  - `BuildingPortraitSet` struct: `barracks` 필드만 (miningPost 제외)
  - `_miningPostPortrait` Sprite (팀 무관 고정)

## 네트워크 미완성 항목
- 상세 목록: [network-todo.md](network-todo.md) 참조
