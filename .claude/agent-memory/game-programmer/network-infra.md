---
name: network-infra
description: 네트워크 인프라 Phase 1~8 상세 구현 내용 (UGS, NGO, 동기화, UI/UX)
type: project
---

# 네트워크 인프라 상세

## Phase 1 — UGS + NGO 기본 설정
- 패키지: `com.unity.netcode.gameobjects` 2.8.1, `com.unity.services.multiplayer` 2.0.0 (Lobby/Relay/Auth 통합)
- 파일 위치: `Assets/_Project/Scripts/Infrastructure/Network/`
  - `UnityServicesInitializer.cs` — UGS 초기화 + 익명 로그인 (순수 C# 클래스)
  - `LobbyManager.cs` — Lobby CRUD + Heartbeat 코루틴 (순수 C# 클래스)
  - `RelayManager.cs` — Relay 할당/참가 + UnityTransport 설정 (순수 C# 클래스)
  - `NetworkGameManager.cs` — 전체 세션 흐름 관리 (MonoBehaviour, DontDestroyOnLoad)

### 핵심 API 매핑
- LobbyService.Instance: `Unity.Services.Lobbies.LobbyService`
- RelayService.Instance: `Unity.Services.Relay.RelayService`
- AuthenticationService.Instance: `Unity.Services.Authentication.AuthenticationService`
- UnityServices.InitializeAsync(): `Unity.Services.Core.UnityServices`
- Allocation → RelayServerData: `allocation.ToRelayServerData("dtls")`
- UnityTransport.SetRelayServerData(): `Unity.Netcode.Transports.UTP.UnityTransport`
- Relay 프로토콜: 모바일="dtls", WebGL="wss"

### Lobby 데이터 컨벤션
- Relay Join Code 키: `LobbyManager.RelayJoinCodeKey = "RelayJoinCode"`
- DataObject.VisibilityOptions.Public 필수

### NetworkGameManager 흐름
- Host: InitializeAsync → HostGameAsync → [Relay → Lobby → StartHost()]
- Client: InitializeAsync → JoinGameAsync → [Lobby 참가 → RelayJoinCode → JoinRelay → StartClient()]
- 에디터: NetworkManager GameObject + UnityTransport 컴포넌트 씬 배치 필요

## Phase 2 — 팀 할당 + 게임 시작
- `LocalPlayerTeam.cs` — 정적 팀 홀더 (싱글=Blue, 네트워크 시 갱신)
- `TeamAssigner.cs` — NetworkBehaviour, Player Prefab에 부착, Host=Blue/Client=Red
  - NetworkVariable<int> _assignedTeamIndex (Server Write Only)
- `NetworkGameFlow.cs` — 씬 NetworkObject, 모든 플레이어 준비 → StartGameClientRpc
- `GameBootstrapper.StartNetworkGame(TeamId)` — 네트워크 전용 진입점

### 팀 매핑
- TeamId: Neutral=0, Blue=1, Red=2
- Host(OwnerClientId=0)→Blue, Client→Red
- TeamAssigner._assignedTeamIndex: 0=Blue, 1=Red (TeamId와 다름!)
- NetworkBuildingController: TeamId 정수값 직접 전송 (Blue=1, Red=2)

### GameBootstrapper Start() 분기
- NetworkManager null 또는 IsHost/IsClient=false → 싱글플레이 (LoadMap 즉시)
- 네트워크 → 맵 로드 건너뜀, NetworkGameFlow가 StartNetworkGame() 대기

## Phase 3 — 타일/자원 동기화
- `TileOwnershipData.cs` — INetworkSerializable (Q, R, TeamIndex)
- `NetworkTileSync.cs` — 서버: OnTileOwnerChanged → BroadcastTileChangeClientRpc
- `NetworkResourceSync.cs` — NetworkVariable<int> _blueGold/_redGold (Server Write Only)
- `GameBootstrapper` — GetGrid()/GetResource() public 메서드
- 타이밍 주의: 스폰 시 HexGrid/ResourceUseCase null 가능 → null 방어 필수

## Phase 4 — 건물 배치 동기화
- `NetworkBuildingController.cs` — RequestBuildServerRpc → SpawnBuildingClientRpc
- `BuildingData` — ID 지정 생성자 오버로드 (ID 충돌 방지)
- `BuildingPlacementUseCase` — PlaceBuildingWithId (클라이언트 재생성 전용)
- `BuildingPlacementUI` — 멀티플레이 시 RPC, 싱글 시 기존 흐름

## Phase 5 — 유닛 생산 동기화
- `NetworkProductionController.cs` — RequestEnqueueServerRpc → SpawnUnitClientRpc
  - ProductionStartedClientRpc, SyncQueueStateClientRpc
- `UnitData` — ID 지정 생성자 오버로드
- `UnitSpawnUseCase` — SpawnUnitWithId (클라이언트 재생성)
- `ProductionTicker.Update()` — 서버: Tick+TickIncome+TickSiege / 클라이언트: TickProgressOnly+TickSiege

## Phase 6 — 유닛 이동 + 전투
- `NetworkUnitMovementController.cs` — 클라이언트 예측 이동 + 서버 검증
  - BroadcastServerMove (AI 이동 전용, 모든 클라이언트 전파)
- `NetworkCombatController.cs` — 서버 권한 전투, 유닛별 개별 쿨다운
  - OnNetworkSpawn: NetworkContext.Set() / OnNetworkDespawn: Reset()
- `NetworkHealthSync.cs` — SyncHealthClientRpc (HP 차이 보정)
- AI 이동 서버 권한: ProductionTicker에 _networkMovement 주입, 클라이언트는 BroadcastMoveClientRpc 수신

## Phase 7 — 승패 판정
- `NetworkGameEndController.cs` — AnnounceWinnerClientRpc + ForceWin()
- 설계 원칙:
  - 싱글: GameEndUseCase → OnGameEnd → GameEndUI.OnGameEnd
  - 멀티: 서버 OnGameEnd → NetworkGameEndController → AnnounceWinnerClientRpc → ShowResult(localTeam 기준)
  - 클라이언트 GameEndUseCase는 OnGameEnd 발행 안 함
- **로비 복귀 설계 (2026-03-17 변경)**:
  - RPC 기반 로비 복귀 제거됨 — 각 클라이언트가 독립 로컬 처리
  - `GameEndUI.ReturnToLobby()`: NetworkManager.Shutdown() → SceneManager.LoadScene("Lobby")
  - `GameEndUI.CountdownCoroutine()`: 30초 자동 복귀, WaitForSecondsRealtime(1f) (timeScale=0 대응)
  - `_countdownText` SerializeField: Inspector 연결 필요 (null 체크 있음)

## Phase 8 — UI/UX 네트워크 대응
- `GameHudUI.cs` — 적팀 골드 표시, LocalPlayerTeam.Current 기준
- `NetworkStatusUI.cs` — RTT 표시 (UnityTransport.GetCurrentRtt)
- `ReconnectionHandler.cs` — 30초 대기 후 ForceWin()
- `LobbyUI.cs` — HostGameAsync/JoinGameAsync, 중복 입력 방지

### RTT API
- `UnityTransport.GetCurrentRtt(ulong clientId)` — ulong ms 단위
- `Unity.Netcode.Transports.UTP` 네임스페이스
- NetworkConfig.NetworkTransport as UnityTransport 캐스팅

## NetworkContext 패턴
- 파일: `Application/NetworkContext.cs`
- Application 레이어 → Unity.Netcode 직접 참조 방지 (정적 홀더)
- NetworkCombatController.OnNetworkSpawn() → NetworkContext.Set(IsServer, true)
- UnitCombatUseCase: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer` 분기

## GameBootstrapper 공개 접근자 전체
- GetGrid(), GetResource(), GetBuildingPlacement(), GetConfig()
- GetUnitProduction(), GetUnitSpawn(), GetPopulation()
- GetMovement(), GetCombatUseCase(), GetUnitFactory(), GetGameEndUI()

## 동기화 타이밍 주의
- NetworkTileSync/ResourceSync 스폰 시 HexGrid/ResourceUseCase null 가능 → null 방어
- ResourceUseCase 생성자는 OnResourceChanged 미발행 → SyncInitialGold() 필요
- AddGold(team, negativeAmount) = 골드 감소

## 중요 교훈
- `com.unity.services.multiplayer` 2.0.0 은 Lobby+Relay+Auth 통합 패키지
- NetworkBehaviour는 Infrastructure 레이어에만
- LobbyService.Instance 사용 전 UnityServices.InitializeAsync() 완료 필요
- 씬 배치 NetworkObject는 StartHost() 시 자동 스폰
- Domain 최소 수정 원칙: ID 지정 생성자 오버로드 패턴
- 서버 PlaceBuilding() 시 이미 GameEvents 발행 → SpawnBuildingClientRpc에서 IsServer 체크 필수
