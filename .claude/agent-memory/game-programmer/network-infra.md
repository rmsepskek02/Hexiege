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

## 연구소 강화(Research/Upgrade) 네트워크 동기화 — 버그 패턴 (2026-07-31)
- 구조: `NetworkUpgradeController`(Infra) ↔ `UnitUpgradeUseCase`(App, `_active`=진행중, `_levels`=팀별 트랙 레벨).
  - 착수: 클라 `TryResearch`→`RequestResearchServerRpc`→서버 `TryStartResearch`→`ResearchStartedClientRpc`(요청 클라만, `OnResearchStartedLocal` 직접 발행).
  - 완료: 서버 `TickResearch`→`OnResearchCompleted` 훅→`ResearchLevelClientRpc`(양 클라 브로드캐스트)→클라 `SetLevel`→`OnUpgradeChanged`.
  - 취소: `RequestCancelResearchServerRpc`→`CancelResearchByBuilding`(buildingId 기준)→`ResearchCanceledClientRpc`(`OnUpgradeChanged` 직접 발행).
- **핵심 버그(고침)**: MP 클라에서 "완료 후 진행 레이어→매트릭스 복귀 안 됨". 착수/취소 ClientRpc는 `GameEvents`를 **직접** 발행해 서비스 의존이 없지만, 완료(`ResearchLevelClientRpc`)만 `_services.GetUpgradeUseCase().SetLevel()`을 타서 비대칭. `_services`가 스폰 레이스로 null이면 완료만 조용히 조기 반환→패널이 진행 레이어에 갇힘. MP는 데미지가 서버 권위라 클라 `_levels`가 UI 표시에만 쓰여 이 null이 이 버그로만 드러남.
  - 진단 지문: **착수 표시는 되는데 완료만 안 되면** → 그 경로만 `_services`(캐시)에 의존하는지 의심.
  - 수정: `ResolveServices()`(=`_services ??= GameServicesLocator.Current`)로 지연 재조회 + 서비스 끝내 null이어도 `OnUpgradeChanged` 직접 발행(취소 경로와 대칭).
- **씬 NetworkObject 스폰 레이스**: `OnNetworkSpawn`에서 `GameServicesLocator.Current`를 1회만 캐시하면, 컨트롤러가 `GameBootstrapper.Register` 전에 스폰될 때 null로 굳음. 사용 시점 지연 재조회가 안전 패턴.
- **자연회복(Regen)**: 그룹 무관 트랙. `UnitUpgradeUseCase.Key()`가 `stat==Regen`이면 그룹을 `UpgradeGroupHelper.RegenCanonicalGroup(=TransPlant)`로 정규화. UI(`ResearchMatrixView`)도 Regen 셀을 group=RegenCanonicalGroup로 바인딩. 서버가 Regen을 거부하는 별도 경로는 **없음**(공/방/속과 동일). "MP Regen 안 됨"은 위 완료-클리어 버그가 패시브 효과라 "업그레이드 안 됨"처럼 보인 것 + 서버 완료 후 취소 시도라 `_active` 비어 "취소 불가".
- **건물 배치 아이콘(BuildingPlacementUI)**: `_blue/redTranscendenceBuildings` 등 6개 `List<BuildingPortraitEntry>{type,icon}`는 **Inspector 직렬화 데이터**. `UpdateButtonPortraits`가 `icon.sprite=entry.icon` 대입만 함. 아이콘 누락=순수 Inspector(코드 아님). AncientGrove=BuildingType.Research(=4), 초월 연구소도 같은 타입.
- **WireUpgradeSystem 하베스트 함정**: 생산 패널에서 앵커를 하베스트해 `SetRect`(sizeDelta=0)로 적용 → 원본이 포인트 앵커(min==max)면 0×0 무형 요소가 됨. `IsUsableStretchAnchors`로 가드(철거 버튼/환불 텍스트). 철거 버튼 배선(`_demolishButton`/`_demolishRefundText`)은 `BuildPanel`에 이미 존재 → 안 보이면 Wire 재실행 필요할 수 있음.
