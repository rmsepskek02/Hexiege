# Plan: 멀티플레이 로비 복귀 버그 수정

**날짜:** 2026-03-17

---

## 수정 목표

커스텀/랜덤 매칭 게임 종료 후 "로비로" 버튼 클릭 시 정상적으로 Lobby 씬으로 이동.

---

## 수정 파일

| 파일 | 작업 |
|------|------|
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs` | BackToLobbyClientRpc 수정 |
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` | ConnectedPlayers 리셋 추가 |

---

## 구현 1: NetworkGameEndController.cs

### 수정 전

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

### 수정 후

```csharp
[ClientRpc]
private void BackToLobbyClientRpc()
{
    Time.timeScale = 1f;

    // 모든 클라이언트에서 NGO 연결 해제 후 Lobby 씬 전환
    // OnMultiplayerRestart()와 동일한 패턴
    if (NetworkManager.Singleton != null)
        NetworkManager.Singleton.Shutdown();

    SceneManager.LoadScene(_lobbySceneName);
}
```

**이유**: NGO 연결이 살아있는 채로 씬 전환 시 OnClientConnectedCallback 등 구독이 유지되어 게임 씬 재로드 트리거 가능.

---

## 구현 2: BattleViewModel.cs

### 수정 위치

`OnNetworkError()` 또는 별도 `ResetConnectionState()` 메서드에 ConnectedPlayers 리셋 추가.
또는 `DisconnectAsync()`가 호출될 때 리셋.

Shutdown() 이후 Lobby 씬으로 이동 시 BattleViewModel이 재초기화되면 자동으로 리셋되지만,
DontDestroyOnLoad인 경우 명시적 리셋 필요:

```csharp
// NetworkGameManager.DisconnectAsync() 또는 씬 전환 전 호출되는 위치에서
ConnectedPlayers.Value = 0;
```

**단, Shutdown() 후 SceneManager.LoadScene("Lobby")로 씬이 재로드되면
BattleViewModel이 새로 생성되므로 자동 리셋됨 → 별도 수정 불필요할 수 있음.**
구현 1만으로 해결 가능한지 테스트 후 판단.

---

## 위험 요소

| 위험 | 평가 | 대응 |
|------|------|------|
| Shutdown() 후 ClientRpc가 전달 안 됨 | 낮음 | ClientRpc는 이미 수신된 상태에서 Shutdown() 호출이므로 문제 없음 |
| Lobby 씬에서 NetworkManager 중복 | 낮음 | Shutdown() 후 DontDestroyOnLoad NetworkManager 정리됨 |
| DisconnectAsync() 중복 호출 | 낮음 | Shutdown()은 멱등성 보장 |

---

## 테스트 체크리스트

- [ ] Host가 "로비로" 버튼 클릭 → 양쪽 모두 Lobby 씬으로 이동 확인
- [ ] Client가 "로비로" 버튼 클릭 → 양쪽 모두 Lobby 씬으로 이동 확인
- [ ] 로비 복귀 후 게임이 재시작되지 않음 확인
- [ ] 로비 복귀 후 다시 매칭/커스텀 게임 정상 시작 확인
- [ ] 싱글플레이 로비 복귀는 영향 없음 확인
