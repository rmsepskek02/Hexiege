# Game Programmer Agent Memory

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
  - 클라이언트: SpawnUnitWithId(id, ...) → OnUnitSpawned + OnUnitProduced 발행 → UnitFactory 프리팹 + ProductionTicker 랠리 이동
  - 실패 시: EnqueueFailedClientRpc로 요청자에게만 피드백
- `UnitData` — ID 지정 생성자 오버로드 추가 (int id 선두 파라미터, BuildingData와 동일 패턴)
- `UnitSpawnUseCase` — SpawnUnitWithId(id, type, team, coord) 추가
  - 클라이언트 측 재생성 전용: IsWalkable/중복 검증 생략, 이벤트 발행 포함
- `ProductionTicker.Update()` — 서버 전용 분기 추가
  - NetworkManager.Singleton.IsListening && !IsServer → Tick/TickIncome 생략, TickSiege만 실행
  - Siege 시스템은 클라이언트에서도 유닛 시각 이동이 필요하므로 분기 없이 실행
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
  - 서버 Update: _attackInterval마다 모든 유닛 TryAttack() 일괄 처리
  - 서버: OnEntityDied 구독 → EntityDiedClientRpc(entityId, isUnit) 전파
  - 클라이언트: HandleUnitDied / HandleBuildingDied → TakeDamage(HP 소진) → RemoveUnit/Building → OnEntityDied 재발행
- `NetworkHealthSync.cs` — NetworkBehaviour, 씬에 NetworkObject 배치 (Phase 6에 구현 완료)
  - 서버: OnEntityDamaged 구독 → SyncHealthClientRpc(entityId, isUnit, serverHp)
  - 클라이언트: 현재 HP와 서버 HP 차이만큼 TakeDamage로 맞춤

## NetworkContext 패턴 (Application 레이어용 네트워크 상태 홀더)
- 파일: `Assets/_Project/Scripts/Application/NetworkContext.cs`
- 목적: Application 레이어가 Unity.Netcode(NetworkManager)에 직접 의존하는 것을 방지
- 사용 패턴: NetworkCombatController.OnNetworkSpawn() → NetworkContext.Set(IsServer, true)
- UnitCombatUseCase.TryAttack(): `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return false;`
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

## 유닛별 개별 이동속도
- UnitData.MoveSeconds (float, readonly) — 타일 1칸 이동 소요 시간
- UnitStats.GetMoveSeconds(UnitType) — 타입별 기본값 (Pistoleer=0.8, default=0.3)
- UnitView.MoveAlongPath: _unitData.MoveSeconds 참조 (GameConfig.UnitMoveSeconds 대신)
- Pistoleer 스탯: HP=50, Attack=3, Range=1, MoveSeconds=0.8

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

- sortingOrder 계층 (FlatTop 기준):
  - 타일: 0~29 | 금광: 1~30 | 건물: 51~79 | 유닛: 100(고정)
