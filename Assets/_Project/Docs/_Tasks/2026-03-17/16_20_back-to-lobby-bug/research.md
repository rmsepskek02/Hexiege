# Research: 멀티플레이 로비 복귀 버그

**날짜:** 2026-03-17

---

## 증상

커스텀/랜덤 매칭 게임 종료 후 "로비로" 버튼 클릭 시 로비로 이동하지 않고 게임이 재시작됨.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Infrastructure/Network/NetworkGameEndController.cs` | 로비 복귀 RPC 처리 |
| `Presentation/UI/GameEndUI.cs` | 로비 복귀 버튼 클릭 핸들러 |
| `Infrastructure/Network/NetworkGameManager.cs` | OnClientConnectedCallback, LoadGameScene() |
| `Presentation/UI/ViewModels/BattleViewModel.cs` | ConnectedPlayers, OnClientConnected 핸들러 |

---

## 코드 분석

### 로비 복귀 흐름

```
[버튼 클릭]
GameEndUI.OnBackToLobbyClicked()
  → NetworkContext.IsNetworkActive 체크
  → NetworkGameEndController.RequestBackToLobby()
    → IsServer: BackToLobbyClientRpc() 직접 호출
    → IsClient: RequestBackToLobbyServerRpc() → BackToLobbyClientRpc()
```

### BackToLobbyClientRpc() 현재 코드

```csharp
[ClientRpc]
private void BackToLobbyClientRpc()
{
    Time.timeScale = 1f;
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
    {
        NetworkManager.Singleton.SceneManager
            .LoadScene(_lobbySceneName, LoadSceneMode.Single);
    }
}
```

### OnMultiplayerRestart() 현재 코드 (재시작 버튼)

```csharp
private void OnMultiplayerRestart()
{
    Time.timeScale = 1f;
    if (NetworkManager.Singleton != null)
        NetworkManager.Singleton.Shutdown();  // ← Shutdown() 있음
    SceneManager.LoadScene(_lobbySceneName);
}
```

---

## 버그 원인

### 1. BackToLobbyClientRpc에 Shutdown() 누락

- `OnMultiplayerRestart()`는 `Shutdown()` 후 `SceneManager.LoadScene()` 호출
- `BackToLobbyClientRpc()`는 `Shutdown()` 없이 NGO SceneManager로만 씬 전환
- NGO 연결(Host/Client 상태)이 살아있는 채로 Lobby 씬으로 이동

### 2. ConnectedPlayers 미리셋

- `BattleViewModel.ConnectedPlayers.Value`가 이전 게임 값(2)인 채로 유지
- Lobby 씬으로 돌아온 후 `OnClientConnected` 이벤트가 재발행되면 `>= 2` 조건 충족
- `LoadGameScene()` 재호출 → 게임 씬이 다시 로드됨

### 3. OnClientConnectedCallback 구독 유지

- `NetworkGameManager.HandleClientConnected`가 `OnClientConnectedCallback`에 구독된 채 유지
- Lobby 씬에서 네트워크 이벤트 발생 시 예상치 못한 게임 씬 재로드 가능

---

## 수정 방향

`BackToLobbyClientRpc()`를 `OnMultiplayerRestart()`와 동일한 패턴으로 수정:
- 모든 클라이언트에서 Shutdown() 호출
- Unity SceneManager로 Lobby 씬 로드 (NGO SceneManager 아님)
- ConnectedPlayers 리셋은 BattleViewModel Dispose/재초기화 시 처리
