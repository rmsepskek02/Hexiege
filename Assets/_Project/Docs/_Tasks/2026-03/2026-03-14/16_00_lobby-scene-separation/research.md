# Research: Lobby/Game 씬 분리

**날짜:** 2026-03-14
**작업자:** Claude

---

## 1. 현재 상태

### 씬 구조
- 씬 1개(`Assets/_Project/Scenes/Game.unity`)만 존재
- 로비 UI와 게임 화면이 동일 씬에서 UI 토글 방식으로 공존
- `ProjectSettings/EditorBuildSettings.asset`에 `Game.unity` 하나만 등록됨

### 현재 흐름
```
Game.unity 로드
  ├─ NetworkGameManager.Awake() → DontDestroyOnLoad
  ├─ GameBootstrapper.Start() → 멀티플레이면 대기, 싱글이면 즉시 LoadMap()
  └─ LobbyUI.Start() → LobbyPanel 표시
       ↓ (Host/Join 버튼)
  NetworkManager.StartHost() / StartClient()
       ↓ (2명 연결)
  LobbyUI.OnClientConnectedCallback() → LobbyPanel.SetActive(false)
       ↓
  NetworkGameFlow.OnNetworkSpawn() → WaitForTeamAndSendReady()
       ↓ (양쪽 준비 완료)
  StartGameClientRpc() → GameBootstrapper.StartNetworkGame()
       ↓
  LoadMap(FlatTop) → 게임 시작
```

---

## 2. 핵심 컴포넌트 분석

### NetworkGameManager (DontDestroyOnLoad)
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs`
- **역할**: UGS 초기화, Relay/Lobby 생성, NGO 시작
- **Awake()에서 DontDestroyOnLoad 호출** → 씬 전환 후에도 유지됨
- **이벤트**: OnHostStarted(code), OnClientConnected, OnError, OnDisconnected
- Lobby 씬에 배치하면 Game 씬까지 그대로 유지됨

### LobbyUI (씬 종속)
- **파일**: `Assets/_Project/Scripts/Presentation/UI/LobbyUI.cs`
- **역할**: Host/Join 버튼, 로비 코드 입력/표시
- **의존성**: NetworkGameManager 참조 (씬 내 직접 참조)
- `OnClientConnectedCallback(id)` — connectedCount >= 2 시 LobbyPanel 숨김
- 씬 전환 시 **Lobby 씬에 남아서 소멸**시키면 됨 (Game 씬에서 불필요)

### NetworkGameFlow (NetworkBehaviour, 씬 오브젝트)
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs`
- **역할**: 양 플레이어 준비 신호 수집 → GameBootstrapper.StartNetworkGame() 트리거
- **Game 씬에 배치 필요** — NetworkObject로 씬에 배치되어야 함
- `IsNetworkGameStarted` 플래그로 재스폰 중복 방지 (유지 필요)

### GameBootstrapper (씬 종속)
- **파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- **역할**: 모든 게임 시스템 초기화 (UseCase, Grid, UI, 건물)
- **Game 씬에 배치** — 씬 로드 후 Start()에서 싱글플레이 여부 판단
- 멀티플레이: `NetworkGameFlow.StartGameClientRpc()` → `StartNetworkGame()` 호출 시까지 대기
- `_networkGameStarted` 중복 방지 플래그 유지

### 기타 NetworkBehaviour 컴포넌트
모두 Game 씬에 배치 (현행 유지):
- NetworkBuildingController, NetworkProductionController
- NetworkUnitMovementController, NetworkCombatController
- NetworkHealthSync, NetworkResourceSync, NetworkTileSync
- NetworkGameEndController, ReconnectionHandler

---

## 3. 씬 전환 방식 비교

### Option A: NGO SceneManager (권장)
```csharp
NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
```
- **장점**: 호스트가 Game 씬 로드 → 모든 클라이언트 자동 동기화
- **장점**: Enable Scene Management = ON이므로 기존 설정과 일치
- **장점**: NetworkObject(씬 배치)들이 새 씬에서 자동 재등록됨
- **단점**: 호스트만 호출해야 함 (`IsServer` 체크 필요)

### Option B: 일반 SceneManager
```csharp
SceneManager.LoadScene("Game");
```
- **단점**: 클라이언트에게 자동 전파 안 됨 → 별도 ClientRpc 필요
- **단점**: NGO와 충돌 가능성, 현재 설정(Enable Scene Management ON)과 불일치

**결론**: Option A 채택

### 호출 시점
현재: `LobbyUI.OnClientConnectedCallback()` → 2명 연결 시 LobbyPanel 숨김
변경: `LobbyUI.OnClientConnectedCallback()` → 2명 연결 시 **씬 전환 트리거**

단, NGO SceneManager 호출은 서버(호스트)만 가능:
```csharp
// LobbyUI → NetworkGameManager를 통해 호출
if (NetworkManager.Singleton.IsServer)
    NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
```

---

## 4. NetworkGameFlow 타이밍 분석

씬 분리 후 흐름:
```
Lobby Scene 로드
  → NetworkGameManager (DontDestroyOnLoad) 배치
  → NetworkManager.StartHost() / StartClient()
  → 양쪽 연결 완료

Host: NetworkManager.SceneManager.LoadScene("Game")
  → 모든 클라이언트 Game Scene 동기화 로드
  → NetworkGameFlow (씬 오브젝트) OnNetworkSpawn() 호출
  → WaitForTeamAndSendReady() → 양쪽 준비
  → StartGameClientRpc() → GameBootstrapper.StartNetworkGame()
```

**중요**: Game 씬 로드 시 NetworkGameFlow가 OnNetworkSpawn()을 자동 호출하므로
현재 코드의 `IsNetworkGameStarted` 플래그 로직은 그대로 유지해야 함.

---

## 5. GameBootstrapper.Start() 분기 분석

```csharp
// 현재 GameBootstrapper.Start()
private void Start()
{
    if (!NetworkContext.IsNetworkActive)
    {
        LoadMap(HexOrientation.FlatTop);  // 싱글플레이: 즉시 로드
    }
    // 멀티플레이: NetworkGameFlow.StartNetworkGame() 호출 대기
}
```

씬 분리 후:
- Game 씬이 로드될 때 `NetworkManager.IsHost` 또는 `IsClient`가 이미 true
- `NetworkContext.IsNetworkActive`가 true → 즉시 로드하지 않고 대기
- NetworkGameFlow.OnNetworkSpawn() → 준비 신호 교환 → StartNetworkGame() 정상 호출

**결론**: GameBootstrapper.Start() 로직 변경 불필요

---

## 6. GameEndUI 씬 전환 (선택적)

현재:
- 싱글플레이: 다시하기 → GameBootstrapper.LoadMap() (맵 재로드)
- 멀티플레이: NetworkGameEndController.RestartGame() → NetworkManager.Shutdown() → SceneManager.LoadScene("Game")

씬 분리 후 로비로 돌아가는 흐름 추가 필요:
- "로비로 돌아가기" 버튼 → NetworkManager.Shutdown() → SceneManager.LoadScene("Lobby")
- `NetworkGameEndController`에 `BackToLobbyClientRpc()` 추가 (멀티플레이용)

---

## 7. 영향 범위 정리

### 수정 필요 파일
| 파일 | 변경 내용 |
|------|---------|
| `LobbyUI.cs` | 씬 전환 트리거 추가 (OnClientConnectedCallback → LoadScene 호출) |
| `NetworkGameManager.cs` | `LoadGameScene()` 메서드 추가 (NGO SceneManager 래퍼) |
| `GameEndUI.cs` | "로비로" 버튼 + BackToLobby 로직 추가 |
| `NetworkGameEndController.cs` | BackToLobbyClientRpc 추가 (멀티플레이 로비 복귀) |
| `ProjectSettings/EditorBuildSettings.asset` | Lobby.unity 씬 추가 등록 |

### 새로 생성 파일
| 파일 | 내용 |
|------|------|
| `Assets/_Project/Scenes/Lobby.unity` | 신규 로비 씬 |

### 변경 불필요 파일
- `GameBootstrapper.cs` — Start() 분기 그대로
- `NetworkGameFlow.cs` — OnNetworkSpawn 로직 그대로
- 모든 NetworkBehaviour 컨트롤러 — Game 씬 그대로 유지
- `GameHudUI.cs`, `BuildingPlacementUI.cs`, `ProductionPanelUI.cs` — Game 씬 그대로

---

## 8. UI 구조 / 프레임워크 현황

### 현재 UI 구조
- 모든 UI가 MonoBehaviour 직접 참조 방식
- 패턴 없음 (각 Panel을 직접 SetActive로 토글)
- UniRx 이벤트 구독은 일부 사용 (GameHudUI 등)

### 문제점
- 씬이 추가되면 씬 간 UI 상태 관리가 복잡해짐
- 각 UI 컴포넌트가 서로를 직접 참조 → 강결합
- 로비 화면 내에서도 여러 하위 패널(초기화중, 호스팅, 참가 등) 전환 시 코드 분산

### 권장 UI 패턴 (다음 섹션 상세 설명)
→ **ScreenManager + IScreen 인터페이스** 패턴 (경량 MVP)
