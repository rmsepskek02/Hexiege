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
- **UI 하베스트 함정(역사적 교훈, 배선 셋업 스크립트는 제거됨)**: 생산 패널에서 앵커를 하베스트해 `SetRect`(sizeDelta=0)로 적용하던 방식은 원본이 포인트 앵커(min==max)면 0×0 무형 요소가 됨 → 스트레치 앵커 여부 가드 필요(철거 버튼/환불 텍스트). 철거 버튼 배선(`_demolishButton`/`_demolishRefundText`)은 `BuildPanel`에 존재하는 런타임 필드 → 씬에서 참조만 연결하면 됨.

## 네트워크 종료(Shutdown) 시점 뒷정리 — 확립된 관례 (2026-08-19)

**`IsServer` 는 "내가 서버 역할인가" 이지 "이 오브젝트가 아직 살아 있는가" 가 아니다.**
`NetworkManager.Shutdown()` 과 씬 NetworkObject 디스폰 사이에 **실측 6~41ms**(4회 표본: 25/27/41/6) 의 창이 있고
(실측 근거: `_Logs/_editor/2026-08-19/RuntimeLog.txt` — 255·692·874·1398행 부근. 코드 주석의 `27ms` 는 그중 한 표본일 뿐이다),
그 구간에서 RPC 를 보내면 `"Rpc methods can only be invoked after starting the NetworkManager!"` 가 난다.

- **관례 형태**: `if (!IsSpawned || !IsServer) return;` — 순서 고정(`IsSpawned` 가 앞).
  단락 평가로 미스폰(싱글플레이) 상태에서 `IsServer` 를 건드리지 않는다.
  선례: `NetworkUnit.cs:291`(`ReapplyAnimStateToView`), `NetworkCombatController.Update`.
- **적용 대상**: ClientRpc 전송 · `NetworkObject.Despawn()` · **NetworkVariable 쓰기**(예방 성격 — 디스폰 후
  NetworkVariable 쓰기가 RPC 와 같은 오류를 내는지는 패키지 소스를 못 열어 미확정).
- **길목이 있으면 길목 한 곳에서 막는다.** `NetworkCombatController.SetUnitAnimState` 에 `if (!IsSpawned) return;`
  한 줄을 두어 호출 지점 5곳(Walk/HealCast/FreezeChanged 핸들러 + `TickCombat` + `OnUnitEnteredCombatHandler`)을 한 번에 덮었다.
- ⚠️ **한 파일에서 한 핸들러만 고치면 같은 버그가 다른 경로로 재발한다.** 구독 목록을 전수로 훑을 것.
- **전수 보강 완료 (2026-08-20, network-guard-sweep)** — 이벤트 구독 진입점 8곳에 같은 형태를 넣었다:
  `NetworkResourceSync.OnResourceChangedOnServer` · `NetworkTileSync.OnTileOwnerChangedOnServer`(둘은 가드 신설) ·
  `NetworkGameEndController.OnGameEndServer` · `NetworkHealthSync.OnEntityDamaged`/`OnEntityHealed` ·
  `NetworkProductionController.OnProductionStarted`/`OnProductionQueueChanged`/`OnUnitProduced`(여섯은 `!IsServer` 대체).
  `Infrastructure/Network/` 21개 파일 중 `GameEvents...Subscribe(` 가 있는 파일은 이 5개뿐이다.
- 🔴 **부호가 반대인 `if (IsServer) return;` 과 혼동 주의.** 그것은 **ClientRpc 수신부**에서 서버의 중복 처리를 막는
  정반대 목적이다. 5파일 합계 10곳(Resource 2 · Tile 1 · Health 3 · Production 4 · GameEnd 0).
  특히 `NetworkTileSync.BroadcastTileChangeClientRpc` 의 것을 잘못 고치면 **클라 타일 색이 통째로 죽는다.**
- 가드에는 **로그를 넣지 않는다** — 가드에 걸리는 것은 정상 종료 흐름이고 상태 *전이* 지점이 아니라
  `LogRules` 1.14 금지 8(매 틱 로깅 금지)에 걸린다.
- 미적용으로 남은 곳(범위 밖, 별도 작업 후보): `NetworkUnit.SetAnimState`(`NetworkUnit.cs:170` — `IsServer` 만 보지만
  유일한 호출부인 `NetworkCombatController.SetUnitAnimState` 가 이미 막혀 중복),
  `NetworkGameEndController` 의 `_localRematch*` 3종(→`ServerRpc`, `IsServer` 블록 **밖** 구독이라 `!IsSpawned` 만 필요),
  `ServerRpc` 계열 전반(호출 주체가 UI 입력이라 성격이 다름),
  `ProductionTicker.Update`(`Presentation` — 종료 가드 없음. 길목으로는 더 근본적이나 동작 변경이라 별도 설계 판단 필요).

#### 실기 결과 (2026-08-24) — **회귀 없음. 단, 8곳 중 2곳만 발화가 확인됐다**

근거 `_Logs/_editor/2026-08-24/RuntimeLog.txt`(13,003행) — **`[ERROR]` 0건 · 3경기 정상 종료 · `게임 종료 — 전투 틱 정지` 1경기에 1회(446행)**.
무작위 매칭이라 **1경기 호스트 → 2·3경기 클라이언트**로 역할이 바뀌어 **클라이언트 쪽 로그를 처음 수집**했다.

- ✅ 서버 발화가 로그에 남은 것: `NetworkGameEndController.OnGameEndServer`(447행 1회) · `NetworkProductionController.OnUnitProduced`(1경기 189회).
- ⚠️ **나머지 6곳은 가드 아래 본문에 호출당 로그가 없어 발화 횟수를 셀 수 없다** — 바로 위 *"가드에는 로그를 넣지 않는다"* 의 **대가**다. 규칙을 바꾸자는 뜻이 아니라, **이 관례를 쓰는 한 「가드가 통과시켰다」는 로그로 증명되지 않는다**는 사실을 알고 있으라는 것.
- 🔴 **`IsServer` 가 새로 붙은 `NetworkResourceSync`·`NetworkTileSync` 의 근거를 섞지 말 것.** 클라 구간의 `클라이언트 골드를 서버 값으로 보정`(6,168건) · `타일 동기화 수신`(733건)은 **상대 호스트가 보낸 것**이라 우리 서버 가드의 근거가 아니다. 서버 근거는 1경기의 `서버 모드로 … 동기화 시작`(37·43행) = **분기 진입·구독 성립**과, `서버 유닛 생산 완료` 189회 + 정상 종료 = **골드가 실제로 흘렀다**는 간접 근거까지다.
- **NGO 스폰 순서 경합은 실재하며 재시도로 흡수된다** — 클라 구간에서 `SpawnUnitClientRpc — UnitView 초기화 지연` **319건**에 `RetryInitializeUnitView — 초기화 성공` **319건**이 1:1 대응하고 **실패 0건**(대기 0.01~0.06초). 호스트 구간에는 **0건**이다. 이 경고는 `bcf45ec1` 과 무관하며 **2026-07-19부터 있던 코드**다(※ `git log -S` = 호출 세션 측정값).

### 게임 종료 후 서버 틱 정지 — `_combatStopped` 패턴

`NetworkCombatController` 가 `GameEvents.OnGameEnd` 를 **서버 전용**으로 구독해 `_combatStopped=true` 로 만들고,
`Update` 진입부가 `if (!IsSpawned || !IsServer || _combatStopped) return;` 로 걸러낸다(+`StopAllCoroutines()`).
수정 전에는 승패 확정(`13:33:58.860`) → `Shutdown`(`13:34:01.467`) 사이 **2.6초**간 전투 틱이 계속 돌았다.

- **구독 해제 방식은 기각**. `GameEndUseCase.cs:79` 가 `OnBuildingDied` **디스패치 도중 동기적으로** `OnGameEnd` 를
  발행하므로, 핸들러 안에서 `Dispose()` 하면 디스패치 중 구독자 목록을 바꾸게 된다 → 구독 순서에 따라
  게임을 끝낸 성의 `EntityDiedClientRpc` 가 영영 안 나갈 수 있다. **틱만 멈추고 구독은 유지**가 정답.
- **`GameEndUseCase.IsGameOver` 폴링도 기각** — `IGameServices` 에 접근자가 없고, 무엇보다
  멀티 포기(`NetworkGameEndController.ForfeitServerRpc:311`)는 `GameEndUseCase` 를 거치지 않는다.
  `OnGameEnd` 구독은 정상 종료·포기 **두 경로를 모두** 덮는다.
- `OnGameEnd` 는 순수 클라에서도 재발행된다(`AnnounceWinnerClientRpc`, `!IsServer` 분기) — 서버 전용 구독이라 무관.
- 서버에서 2회 발행 가능(정상 종료 / 포기 — 별개 플래그) → 플래그 세우기·`StopAllCoroutines()` 모두 멱등이라 무해.
  **별도 중복 가드를 두지 않는다.**
- ⚠️ **`TickCombat` 은 "전투"보다 넓다.** 방어 타워 · 파도 · HoT · 자연회복 · **연구 진행** · 스킬 쿨다운 ·
  물안개 · 상태효과가 전부 그 안에 있다(`TickCombat` 359~415행). 멈추면 이 8개가 함께 멈춘다.
- 🔴 **최대 위험 — 플래그 리셋 누락.** `true` 로 남은 채 재경기가 시작되면 위 8개가 전부 멈추고
  성이 파괴될 수 없어 **게임이 영원히 끝나지 않는다.**
  → **`OnNetworkSpawn`(IsServer 분기) + `OnNetworkDespawn` 양쪽에서 `false` 로 초기화.**
  같은 파일의 `_attackTimer` / `_lastCarry` 가 정확히 그 두 자리에서 리셋되므로 **그 옆줄에 붙인다**
  ("이 자리는 경기마다 리셋하는 자리" 가 눈에 보이게).
- ⚠️ **리셋의 실기 검증은 2026-08-19 「재경기 2회 연속 통과」가 유일하다 — 2026-08-24 세션은 이것을 재확인하지 못했다.**
  그 세션은 **2·3경기에 에디터가 클라이언트**였고, 가드가 `if (!IsSpawned || !IsServer || _combatStopped) return;` 이라
  **`_combatStopped` 를 평가하기 전에 `!IsServer` 에서 반환**된다. 2·3경기 사망 로그는 전부 `EntityDiedClientRpc 수신 → 클라 처리`
  경로였고(`서버: 유닛 사망` 0건) **상대 호스트의 틱이 돈 것**이다. **재확인 조건: 에디터가 호스트로 연속 2경기.**
  > **여기서 얻을 교훈:** 단락 평가로 앞 조건에서 반환되는 가드는, **뒷 조건이 실제로 평가되는 구간이 로그에 있어야만** 검증된다.
- 재경기 경로: `NetworkGameEndController.StartRematch`(432~481행)는 동적 NetworkObject 만 명시 Despawn 하고
  씬 오브젝트(`IsSceneObject==true`)는 건드리지 않은 채 `SceneManager.LoadScene("Game", Single)` 로 맡긴다.
  NGO 가 인스턴스를 재사용하든 새로 만들든 **어느 쪽이어도 안전한 형태**를 택한 것.
