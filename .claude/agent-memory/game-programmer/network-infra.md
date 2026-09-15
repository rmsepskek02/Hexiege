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

---

## 무작위 맵 3단계 B — `NetworkMapTransfer` 골격 (2026-09-14, 동작 무변경)

**이 커밋으로 게임 동작은 한 줄도 바뀌지 않는다.** 신설 2파일은 **호출부 0건**이다.

| 파일 | 성격 | 검증 수단 |
|---|---|---|
| `Infrastructure/Network/MapChunkAssembler.cs` (369행) | 🔴 **일부러 순수 C#** — `using System;` 하나뿐 | **`mcs`/`mono` 로 실제 실행됨** |
| `Infrastructure/Network/NetworkMapTransfer.cs` (536행) | `NetworkBehaviour` | **컴파일 검증 불가 — Unity 에서만** |

### 🔴 왜 재조립기를 `NetworkBehaviour` 밖으로 뺐는가 (이 패턴을 다시 쓸 것)

실전에서 맵 canonical 바이트는 323~343바이트라 **조각이 거의 항상 1개**다. 즉 조각 여러 개·중복·역순
경로는 **실전에서 한 번도 안 도는 코드**가 된다. 그런 코드일수록 최소 한 번은 실제로 돌아 봐야 한다.
Unity 타입을 하나라도 쓰면 이 환경에서 컴파일조차 안 되므로, **계산만 하는 부분을 순수 C# 으로 분리**하면
`mcs`/`mono` 로 입력을 만들어 돌려 볼 수 있다. 2026-09-14 에 6가지 입력 + 경계값으로 **79 assert 전부 통과**했다
(조각 1개 / 여러 개 / 중복 / 역순 / 하나 빠짐 / 길이 불일치, 그리고 매 `Accept` 직후 **부분 데이터 미유출** 확인).
→ ⚠️ **`MapChunkAssembler.cs` 에 `using UnityEngine;` · `using Unity.Netcode;` · `GameLog` 을 넣지 마라.**
  넣는 순간 이 검증 수단이 통째로 사라진다(파일 머리말에도 못 박아 두었다).

### 재조립기 계약 (규칙 16 「부분 데이터 사용 금지」를 API 모양으로 강제)

- **완성 전에 바이트를 꺼낼 수 있는 API 가 아예 없다.** `TryGetAssembled` 는 미완성이면 `false` + `null`.
  "지금까지 받은 것만이라도" 류의 접근자·내부 버퍼 노출 프로퍼티를 **추가하지 마라.**
- `IsComplete` 는 **조각 수와 바이트 합계를 둘 다** 본다(규칙 16 "선언된 크기와 일치한 뒤에야").
- `Accept` 의 검사 순서는 **범위 → null/빈 → 길이 → 중복**. 길이를 중복보다 **앞**에 두는 것이 의도다 —
  뒤에 두면 "이미 받은 자리에 길이가 이상한 조각이 왔다"가 그냥 중복으로 삼켜져 이상 징후가 사라진다.
- 중복 조각의 **내용은 비교하지 않는다.** 먼저 온 것을 남긴다(어긋나면 어차피 해시 대조에서 걸린다).
- 넘겨받은 배열을 **반드시 복사**한다(NGO 버퍼는 재사용될 수 있다). 꺼낼 때도 복사본을 준다.

### `NetworkMapTransfer` — 이번 몫과 F 이후 몫의 경계

- **B 에 있는 것**: 스폰/디스폰 수명, `MapTransferState` 6상태 + `SetState` 전이 로그(같은 값이면 무시 —
  금지 8), `_activeNonce`(회차 번호), RPC 3종, 조각 분할·재조립, **더미 바이트 프로브**.
- **F~I 몫**: 진짜 canonical 바이트 싣기, timeout 10초 감시·1회 재전송, 해시 대조, D 방식 검증,
  `MapHandoff` 심기, 씬 전환 게이트, **스폰 주체 배선**. 코드에 `[F 단계]`/`[G 단계]`/`[H 단계]` 주석으로 표시.
- 🔴 **`[ServerRpc(RequireOwnership = false)]` 가 필수다.** 이 객체는 Host 가 스폰하므로 소유자도 Host 다.
  기본값(소유자만 호출)으로 두면 **정작 답을 보내야 할 Client 가 `MapReadyServerRpc` 를 못 부르고 조용히 막힌다.**
- 두 `ClientRpc` 의 첫 줄은 서버 되돌림 가드다(host 는 서버이자 클라라 자기 조각을 자기가 모으게 된다).
  **가드에는 로그를 넣지 않는다**(정상 흐름 + 금지 8).
- 씬 재로드 생존 조건(A 단계)은 **미확정**이라 `DontDestroyOnLoad` 류를 넣지 않았다.

### 🔴 조각 크기는 아직 「잠정값」이다 — 숫자를 확정한 척하지 마라

규칙 16 이 *"근거 없는 숫자를 근거 없는 다른 숫자로 바꾸지 않기 위해 값은 구현 시 NGO 실측으로 확정"*
하라고 지시한다. 그래서 코드가 **이름으로** 그 사실을 드러낸다:
`IsChunkSizeMeasured = false` · `ProvisionalChunkSizeBytes = 1024`(근거 없음을 주석에 명시).
- 못 잰 두 숫자 = ① NGO RPC 한 번의 **실효** 페이로드 상한 ② **Relay 경유** 실효 MTU.
  ⚠️ 씬의 `UnityTransport.m_MaxPayloadSize` = **6144 는 설정값이지 실효 상한이 아니다**(헤더·writer 오버헤드).
  ⚠️ 로컬 127.0.0.1 측정값을 Relay 값으로 삼지 마라.
- 재는 수단 = `RunTransferProbe(probeBytes, chunkSize)` + 인스펙터 컨텍스트 메뉴. **실전과 같은 RPC 경로**로
  더미 바이트를 보낸다(프로브 전용 RPC 를 따로 만들면 정작 쓰는 경로를 잰 것이 아니게 된다).
- `TransferTimeoutSeconds = 10` · `MaxResendCount = 1` 은 **규칙 16 이 정한 값이라 잠정이 아니다**(처리 코드만 F 몫).

### ⚠️ 프리팹은 만들지 않았다 (사용자 Unity 작업 대기)

- `Assets/_Project/Prefabs/Network/` 폴더 자체가 없고, 프로젝트에 **매니저류 `NetworkObject` 프리팹 선례가 0건**이다.
- 손으로 YAML 을 쓰지 않은 이유 2가지(둘 다 실측): ① `NetworkObject` 의 **`GlobalObjectIdHash`** 는 Unity 가
  에셋 GlobalObjectId 에서 계산해 직렬화하는 값이라 사람이 채울 수 없다(유닛 프리팹 실측: `2291559221` 등).
  ② 신설 `.cs` 두 개에 **`.meta` 가 아직 없다** → 스크립트 GUID 가 존재하지 않아 프리팹이 참조할 대상이 없다.
- 🔴 **`DefaultNetworkPrefabs.asset` 은 두 벌 있다.** `Assets/DefaultNetworkPrefabs.asset`(guid `45609a99…`)와
  `Assets/_Project/Resources/Config/DefaultNetworkPrefabs.asset`(guid `abb45d5667d7ce049847712da2b871b1`).
  **씬 두 개(Game·Lobby)의 NetworkManager 가 참조하는 것은 `Resources/Config` 쪽뿐이다**(각각 45430행·7508행).
  루트 쪽에 등록하면 **아무 효과가 없다.**

---

## 무작위 맵 3단계 F·G·H — `NetworkMapTransfer` 알맹이 (2026-09-14, 여전히 동작 무변경)

> 직전 세션이 사용량 한도로 끊겨 메모리를 못 남겼다. 아래는 **F·G·H 커밋(`1ffe0f2` 까지)** 분이며,
> **I 는 그 아래 별도 절**에 적는다. F~H 시점까지도 `BeginHostMapTransfer` 의 호출자는 **0곳**이었다.

- **F(Host)**: `BeginHostMapTransfer(rootSeed, mapTestModeEnabled)` 가 `MapPreparationUseCase` +
  `ResourcesMapFallbackTemplateSource` 를 **스스로 조립**한다(로비에 `GameBootstrapper` 가 없다 —
  Plan §9-마 의 「조합 루트는 하나」와의 마찰은 **아직 사용자 확인 전**이다).
  준비 결과에 **이미 들어 있는** `CanonicalBytes`/`Hash` 를 그대로 package 로 쓴다 —
  🔴 **여기서 바이트를 새로 만들거나 해시를 다시 계산하지 않는다.** 두 번 계산하는 순간
  「어느 쪽이 진짜인가」가 생기고 해시 대조가 의미를 잃는다.
- **timeout/재전송**: `Update` 가 `WaitingReady` 상태에서만 `Time.realtimeSinceStartup` 마감시각을 본다.
  `MaxResendCount = 1`. 재전송은 **들고 있던 package 를 그대로** 다시 보낸다(`SendHostPackage` 를
  최초 전송과 재전송이 **같은 코드**로 공유).
- **G(Client)**: `VerifyAndAnswer` 의 순서가 계약이다 — **해시 대조 → (프로브면 여기서 끝) →
  역직렬화 → 헤더/본문 형식 버전 대조 → D 방식 검증**. 🔴 **원본 32바이트로 대조**한다
  (로그용 16자 문자열로 비교하면 앞 8바이트만 같아도 통과한다).
- **H**: `MapVerificationUseCase.Verify(payload)` 가 Decode 까지 **전부** 한다.
  호출부에서 따로 `Decode` 하지 않는 이유 = 두 번 해석하면 「검증한 정의」와 「실제로 쓰는 정의」가
  다른 객체가 되어 어긋날 길이 생긴다.
- **Host 는 Client 의 "성공" 신고를 그대로 믿지 않는다.** `MapReadyServerRpc` 가 받은 `clientHash` 를
  `_hostHash` 와 **한 번 더** 대조한다. 안 그러면 「Host/Client 해시 비교」가 Client 의 자기 신고가 된다.
- **결말 로그는 회차당 정확히 한 줄** — `_outcomeLogged` 깃발. 두 번째는 **개발 축 Warn 으로만** 남긴다
  (조용히 삼키면 "결말이 두 번 났다"는 버그를 못 찾는다).
  ⚠️ `MapTransferRetried` 는 이 깃발을 **거치지 않는다**(결말이 아니라 중간 전이).
- **프로브는 운영 지표를 오염시키지 않는다** — package 헤더의 `isProbe=true` 로 Client 가
  역직렬화·검증을 건너뛰고, Host 쪽 성공 결말도 `GameLog.Dev` 로만 남긴다.
  (더미 바이트는 반드시 Decode 에 실패하므로, 안 그러면 실측 1회마다 `MapClientVerificationFailed` 가 쌓인다.)

## 무작위 맵 3단계 I — 씬 전환 게이트 (2026-09-14) 🔴 **여기서 처음 동작이 바뀐다**

**바뀐 파일 5개**: `Domain/Map/MapRootSeed.cs`(신설) · `Bootstrap/GameBootstrapper.cs` ·
`Bootstrap/GameBootstrapper.Map.cs` · `Infrastructure/Network/NetworkGameManager.cs` ·
`Infrastructure/Network/NetworkMapTransfer.cs` · `Presentation/UI/ViewModels/BattleViewModel.cs`.

### 게이트의 모양 — 「주인은 Infrastructure, 매니저는 위임 진입점 하나」

```
BattleViewModel.OnClientConnected (2명)
  └─ NetworkGameManager.BeginMapTransferAndLoadGameScene()      ← 종전엔 여기서 LoadGameScene() 직행
       ├─ 프리팹 동적 Spawn → NetworkMapTransfer
       ├─ transfer.OnHostTransferSucceeded → NetworkGameManager.LoadGameScene()
       ├─ transfer.OnHostTransferFailed    → OnMapTransferFailed 발행(로비 유지)
       └─ transfer.BeginHostMapTransfer(MapRootSeed.Create(), GameConfig.MapTestModeEnabled)
BattleViewModel.OnMapTransferFailed → UIManager.ShowLoading(false) + ErrorMessage
```

- 🔴 **`NetworkGameManager` 를 `NetworkBehaviour` 로 승격하지 않았다**(Plan §4-1 후보 C 탈락).
  959행 매니저 + 로비 UI 직접 참조라 초기화 순서 전체가 영향권이다. **위임 진입점 하나만** 받는다.
- 🔴 **결말 통보는 「한 번만」과 「반드시 한 번은」을 동시에 지켜야 한다.** 깃발이 **두 겹**이다.
  · `NetworkMapTransfer._hostOutcomeNotified` — 전송 쪽 결말 자리 **4곳**(`FailHostRound` ·
    응답=실패 · 응답=해시 불일치 · 응답=성공)에서 `NotifyHostOutcomeOnce` 로 수렴.
  · `NetworkGameManager._mapTransferGateSettled` — 게이트 쪽. 성공/실패 통보 양쪽에 건다.
  **실패 통보 뒤 성공 통보가 따라오면 「실패한 판인데 전투 씬으로 넘어간다」**(규칙 16 정면 위반).
  반대로 아무 통보도 안 가면 **로비가 로딩 화면에서 영영 멈춘다**(실패 팝업이 §10 으로 빠졌으므로
  로딩을 내리는 유일한 통로가 이 이벤트다).
- ⚠️ `_hostOutcomeNotified` 는 `_outcomeLogged` 와 **일부러 별개 깃발**이다. 로그 억제와 게이트 통보가
  서로 다른 이유로 두 번 밟힐 수 있다.
- ⚠️ **프로브 회차는 게이트에 통보하지 않는다**(`NotifyHostOutcomeOnce` 첫 줄 `if (_hostIsProbe) return;`).
  실측 도중 씬이 넘어가면 실측이 끊긴다.
- 🔴 **이때 발견해 함께 고친 것**: `StartHostRound` 의 `PackageTooLarge` 조기 실패 분기가
  `_hostIsProbe` 를 **갱신하지 않아 지난 회차 값이 남았다.** 직전이 프로브면 진짜 맵의 실패가
  프로브로 오인돼 게이트에 통보가 안 가고 로비가 멈춘다. 그 분기에 `_hostIsProbe = isProbe;` 추가.

### 로그 — 누가 어디서 남기는가 (같은 사건을 두 줄로 남기지 않기 위해)

| 사건 | 축 | 남기는 곳 |
|---|---|---|
| 전송 결말 4종(운영 키) | Ops | `NetworkMapTransfer` **한 줄만** |
| 게이트가 막았다 | **Dev/Warn** | `NetworkGameManager.HandleMapTransferFailed` |
| 프리팹 미배선·컴포넌트 누락 | **Dev/Warn** | `NetworkGameManager`(설정 오류 = 개발, LogRules 1.3 원칙 3 단서. 선례: `GameBootstrapper.Map.cs` 의 "GameConfig 가 Inspector 에 연결되지 않아…") |
| 인계 맵 없음(전투 씬) | Ops/Error `MapPreparationFailed` | `GameBootstrapper.ProjectHandedOverMap` |

🔴 **전투 씬에서 `MapTransfer*` 키를 절대 쓰지 않는다.** 그 4종은 「전송 회차 하나는 그중 정확히
하나로 끝난다」는 배타 관계이고 `_outcomeLogged` 가 강제한다. 다른 객체가 같은 키를 한 줄 더
내보내면 그 배타성이 깨져 전송 성공/실패 집계가 조용히 망가진다.

### 폐기(Clear) 배선 — 규칙 14 *"로비 복귀 또는 연결 종료 시 폐기"*

`MapHandoff.Clear()` 의 호출자가 **0건 → 2건**이 됐다: `NetworkGameManager.DisconnectAsync` ·
`NetworkGameManager.BackToLobby`. 같은 자리에서 `_mapTransferInProgress = false` 와
`CleanupMapTransferSubscription()` 도 함께 한다 —
🔴 **`_mapTransferInProgress` 가 true 로 굳으면 다음에 방을 만들어도 "이미 진행 중"으로 판정돼
맵 준비가 아예 시작되지 않고 로딩에서 멈춘다.**

### ⚠️ 프리팹 배선 (사용자 Unity 작업)

- 넣을 자리: **`NetworkGameManager` 의 `_mapTransferPrefab`**(Inspector 표시명 **`Map Transfer Prefab`**,
  헤더 `Random Map`). 그 컴포넌트는 **`Lobby.unity` 의 `NetworkGameManager` 오브젝트**에 붙어 있고
  `Awake` 에서 `DontDestroyOnLoad` 된다.
- 프리팹에는 **`NetworkObject` + `NetworkMapTransfer`** 둘 다 있어야 한다(둘 중 하나라도 없으면
  개발 축 Warn 후 게이트 실패 처리).
- 🔴 **`Assets/_Project/Resources/Config/DefaultNetworkPrefabs.asset` 에 등록**해야 한다.
  루트의 `Assets/DefaultNetworkPrefabs.asset` 은 **씬이 참조하지 않는다** — 거기 등록하면 효과가 없다.
- 배선하지 않으면: 2명 접속 → 로딩이 잠깐 떴다 사라지고 **전투 씬으로 넘어가지 않은 채 로비에 남는다.**
  에디터 콘솔에 「맵 전송 프리팹이 Inspector 에 연결되지 않아…」 개발 Warn 이 뜬다.

---

## 재경기 맵 A~D — 「수락 즉시 씬 재로드」 사이에 맵 준비를 끼워 넣기 (2026-09-15)

> 3단계 I 가 **최초 경기**의 씬 전환 게이트를 만들었고, 여기서 같은 게이트를 **재경기**에도 쓴다.
> 🔴 **D 커밋 하나에서 처음으로 재경기 동작이 바뀐다(A·B·C 는 동작 무변경).**
> 선행: T1~T3(맵 테스트 모드 삭제)은 이 작업 전에 끝나 있었다.

**바뀐 파일 5개**: `Infrastructure/Network/NetworkGameManager.cs`(A·B) ·
`Infrastructure/Network/NetworkMapTransfer.cs`(B) ·
`Infrastructure/Network/NetworkGameEndController.cs`(C·D) ·
`Application/Events/GameEvents.cs`(C) · `Presentation/UI/GameEndUI.cs`(C).
**신설 파일 0개 · 씬/프리팹/에셋 0건 · 새 `LogEvent` 키 0개.**

### 배선 전체 모양 (재경기 쪽)

```
[결과 화면] 수락 / 양측 동시 요청
  └─ NetworkGameEndController.BeginRematchMapPreparation()      ← 종전엔 여기서 StartRematch() 직행
       └─ NetworkGameManager.BeginRematchMapTransfer(성공콜백, 실패콜백)
            └─ BeginMapTransferRound(Rematch, ...)   ← 최초 경기와 **같은 본체**
                 ├─ 프리팹 동적 Spawn(결과 화면 위) → NetworkMapTransfer
                 └─ BeginHostMapTransfer(MapRootSeed.Create(), Rematch)
       성공 → StartRematch()            (🔴 그 메서드는 한 줄도 안 고쳤다 — despawn 루프 포함)
       실패 → HandleRematchMapFailed()  → _rematchRequesterId 초기화 + NotifyRematchMapFailedClientRpc()
                                          → GameEvents.OnNetworkRematchMapFailed
                                          → GameEndUI.RestoreRematchButton()
```

### 🔴 A — 진입점을 가를 때 지킨 것 (다음에 같은 모양을 또 만들 것이다)

- **`BeginMapTransferAndLoadGameScene()` 의 이름·시그니처를 그대로 뒀다.** 호출부
  `BattleViewModel.OnClientConnected` 가 무변경 = **최초 경기 회귀 면적 0**. 본문만
  `BeginMapTransferRound(roundKind, onSucceeded, onFailed)` 로 옮겼다(3단계의
  `PrepareAndProjectMap` 분할 + 래퍼 유지와 같은 형태).
- **결말에 무엇을 할지는 「필드로 들고 있는 콜백 2개」**(`_mapTransferSuccessAction` ·
  `_mapTransferFailureAction`). 전송은 여러 프레임에 걸쳐 일어나 결말이 한참 뒤에 오므로,
  회차를 시작한 쪽이 넘겨 준 것을 이 클래스가 들고 있어야 한다.
- 🔴 **결말 자리에서 「먼저 꺼내 두고 필드를 비운 뒤 부른다」.** 성공 콜백 안에서 씬이 재로드되는
  등 무슨 일이 일어날지 모르는데, 그 안에서 같은 필드를 다시 읽으면 지난 회차 값이 보인다.
- 🔴 **성공 콜백이 없으면 `LoadGameScene()` 으로 폴백하지 않는다.** 누가 시작한 회차인지 모르는 채
  씬을 넘기면, 재경기였을 경우 `StartRematch()` 의 정리 절차를 건너뛴 전투 씬이 열린다.
- 🔴 **실패는 기존 `OnMapTransferFailed` 이벤트를 재경기에 쓰지 않는다.** 그 이벤트의 구독자는
  **로비 UI(`BattleViewModel`)** 이고 재경기 실패는 **결과 화면**이 받아야 한다.
  → `_mapTransferFailureAction != null` 이면 그쪽으로만, null 이면 종전대로 이벤트로.
- **중복 요청 가드에서도 이번 요청의 실패 콜백은 부른다.** 안 부르면 재경기가 「수락했는데 아무 일도
  안 일어나고 버튼도 잠긴 채」 남는다. 최초 경기는 `onFailed == null` 이라 종전과 완전히 같다.
- **콜백 2개를 비우는 자리 4곳**: 성공 결말 · `FailMapTransferGate` · `OnDestroy` ·
  `DisconnectAsync`/`BackToLobby`(`_mapTransferInProgress = false` · `MapHandoff.Clear()` 와 같은 줄).

### B — 회차 표식 `Round=` (새 로그 키를 만들지 않기 위한 필드)

- 신설 `public enum MapTransferRoundKind { First = 0, Rematch = 1, Probe = 2 }`(`NetworkMapTransfer.cs`).
  🔴 **동작을 가르지 않는다. 로그에만 쓴다.** 결말 키를 나누면 전송 성공/실패 집계가 두 벌로 갈라지므로
  (규칙 16 — 두 경기가 같은 전송 경로), **둘을 가려내는 수단이 이 필드 하나뿐**이다.
- **`BuildTransferLogData` 의 `data` 안에 `Round=` 를 넣었다**(`extraFields` 가 아니라).
  이유: 그 메서드는 이미 `sendCount`·`chunkCount`·`totalBytes`·`mapVersion` 을 `IsServer ? host : client`
  로 갈라 싣는다. 같은 모양으로 한 줄 더 넣으면 **호출부 8곳을 하나도 건드리지 않는다** —
  호출부를 고치는 방식은 「한 자리를 빠뜨린다」는 바로 그 사고(아래)를 다시 부른다.
- 🔴 **`_hostRoundKind` 는 `StartHostRound` 의 두 자리 모두에서 갱신한다**(정상 분기 + 용량 초과
  즉시 실패 분기). `_hostIsProbe` 가 3단계 I 에서 정확히 한 자리를 빠뜨려 고쳐진 전례가 있다.
  **실측 확인 방법**: `grep -n "_hostIsProbe = \|_hostRoundKind = "` 가 **각각 2건**이어야 한다.
- **표식을 시작 통보 RPC 로 Client 에도 보낸다**(`MapPrepareBeginClientRpc(..., int roundKind)`).
  Client 도 자기 결말 로그를 남기므로(성공 · `MapClientVerificationFailed`), Client 로그 파일만 보고도
  어느 경기의 전송이었는지 알 수 있어야 한다. **enum 이 아니라 int 로 싣는다** —
  같은 파일 `MapReadyServerRpc` 가 error code 를 int 로 싣는 것과 같은 이유(기본 타입만 쓴다).
- ⚠️ **`_clientRoundKind` 는 `ResetSession()` 에서 되돌리지 않는다** — 회차마다 시작 통보가 반드시
  덮어쓰므로, `First` 로 되돌리면 모르는 값에 거짓을 채워 넣는 셈이다(`_clientIsProbe` 와 다른 판단).
- ⚠️ **`BuildMapPreparationLogData` 에는 `Round=` 를 넣지 않았다.** 그 필드 집합은
  `Bootstrap/GameBootstrapper.Map.cs` 의 같은 이름 메서드(싱글 경로)와 **반드시 같아야** 하고,
  한쪽만 늘리면 같은 키(`MapPreparationSucceeded` 등)의 집계가 조용히 갈라진다.
  → **결과: `MapPreparation*` 3종 키에는 회차 표식이 없다.** 그 회차가 재경기였는지는 바로 뒤에 오는
  개발 축 「맵 전송 회차 시작」 줄의 `Round=` 로 가린다.

### C — 실패 통보 채널을 새로 판 이유

- 신설 `GameEvents.OnNetworkRematchMapFailed`(`Subject<Unit>`). 🔴 **기존 「거절」 이벤트를 재사용하지
  않았다** — 거절 팝업은 *"상대방이 재경기를 거절하였습니다"* 를 띄운다. 맵 실패에 그 문구가 뜨면 거짓말이고,
  한 채널로 합치면 나중에 갈라낼 수 없다.
- `NotifyRematchMapFailedClientRpc()` 는 **대상 지정 없이 양쪽 모두**에게 간다(거절 알림이 요청자
  한 쪽에만 가는 것과 다르다). 수락한 쪽도 「수락했는데 아무 일도 안 일어난」 상태이기 때문이다.
- **`_rematchRequesterId = ulong.MaxValue` 로 되돌린다**(규칙 M-3 *"rematch pending 상태를 초기화"*).
  🔴 안 되돌리면 다음 요청이 **「상대도 이미 요청했다」 분기**로 빠져 맵 준비 없이 곧바로 시작된다.
- 🔴 **실패 경로에서 `MapHandoff` 를 건드리지 않는다.** 실패 회차는 `Set()` 을 부르지 않으므로
  **아무것도 안 하는 것이 곧 「기존 맵 정의 유지」**다. 지우면 이미 확정된 값까지 날아간다.
- **범위 밖(결함 아님)**: 실패 팝업·문구 · 대기 중 표시 · 자동 로비 복귀 카운트다운 재시작.
  ⚠️ 그래서 **규칙 M-3 세 조항 중 「countdown 전체 길이 재시작」은 미충족으로 남는다.**

### D — 뒤집는 자리와 그때 지킨 것

- `AcceptRematchServerRpc` 와 `RequestRematchServerRpc` 의 **상호 동의 분기 두 곳을 반드시 함께**
  고친다. 한쪽만 고치면 「수락으로 시작한 재경기는 새 맵인데 양측 동시 요청은 빈 맵」이 된다.
  두 자리 모두 `// StartRematch();` 로 **주석 비활성화** + 표식 `[재경기맵 대체 대기]`(grep 2건).
- **매니저 탐색은 `FindFirstObjectByType<NetworkGameManager>()`** — 이 프로젝트의 관습이다
  (`LobbyUI` · `LobbyRootView` · `GameEndUI` 가 같은 방식). **새 정적 홀더를 만들지 않았다.**
  `NetworkGameEndController` 는 이미 `OnNetworkSpawn`(서버)에서 캐시해 두므로 그것을 먼저 쓴다.
- 🔴 **매니저를 못 찾으면 실패 처리한다. 절대 `StartRematch()` 로 넘어가지 않는다** —
  맵을 만들 주체가 없는데 씬만 재로드하면 정확히 지금 고치려는 그 버그(빈 전장)가 재현된다.
- ✅ **정리 루프가 저절로 맞아떨어진다**: 전송 객체를 **결과 화면 위에서 새로 스폰**하므로
  `StartRematch()` 의 「동적 스폰 `NetworkObject` 전수 Despawn」 루프가 **동적 스폰이라는 이유 하나로**
  그 객체를 정상 정리한다. 🔴 **그 루프에 예외를 파지 않는 것이 이 설계를 고른 이유 자체다.**
- **전투 씬이 꺼내 쓰는 쪽은 신규 작업 0건** — `GameBootstrapper.PrepareAndProjectMap()` 의 멀티 분기
  (`IsNetworkMode()` → `ProjectHandedOverMap()` → `MapHandoff.TryTake`)가 재경기에도 그대로 탄다.

### 검증 — 무엇이 확인됐고 무엇이 안 됐나

- 🔴 **이 환경에서 5개 파일 전부 컴파일 불가**(`UnityEngine` · `Unity.Netcode` 참조).
  확인한 것은 ① 주석·문자열을 걷어낸 **중괄호 개폐 균형** ② 시그니처 변경의 **호출부 전수 grep**
  ③ **`mcs`/`mono` 하네스**로 옮겨 적은 회차 상태 기계(13 assert ALL PASS) 뿐이다.
- 🔴 **하네스가 확인한 불변식**(그대로 다시 쓸 수 있다):
  최초 성공→씬 로드 1회·로비 실패 0회 / 최초 실패→씬 0회·로비 1회 /
  재경기 성공→`StartRematch` 1회·로비 채널 0회·`LoadGameScene` 0회 / 재경기 실패→`StartRematch` 0회 ·
  복원 1회 / **실패 뒤 늦은 성공 통보→재경기 시작 0회**(규칙 14 보존) /
  진행 중 중복 요청→재경기만 통보 1회, 최초는 종전대로 무시 /
  **재경기 회차가 끝난 뒤 최초 경기 회차가 지난 콜백을 쓰지 않는다** /
  프리팹 미배선→재경기도 반드시 통보 / 프로브→게이트 통보 0회.
  하네스 위치: 이 세션의 scratchpad(`RoundGateHarness.cs`) — 리포지토리에 넣지 않았다.
- ⚠️ **실기에서 관측 불가능한 것**: 실패 시 상태 복원(정상 경로에서 실패가 안 난다) · 폴백 템플릿 ·
  timeout/재전송. **체크리스트에 넣지 말 것**(`.claude/mistakes.md` 2026-09-09).

### ⚠️ 이번에 발견했지만 고치지 않은 것 (범위 밖 — 보고만)

- `NetworkGameManager.cs` 에 **raw `Debug.Log` 3건**(매치메이킹 대기·참가 로그). `LogRules` 의
  「raw Debug.Log 금지」에 어긋나지만 이번 작업과 무관한 기존 코드라 손대지 않았다.
- `MapHandoff.cs` 의 *"이 메서드는 현재 호출자가 0건이다"* 주석(계획서 대체-4)은 여전히 사실이 아니다
  (실제 2건). **사용자 승인 대상**이라 고치지 않았다.
