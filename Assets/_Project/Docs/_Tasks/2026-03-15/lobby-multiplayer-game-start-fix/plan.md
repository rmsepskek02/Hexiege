# Plan: 멀티플레이 게임 시작 버그 수정

**날짜:** 2026-03-15

---

## 변경 파일

`Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs` 단 1개

---

## 변경 내용

### `HostGameAsync()` — StartNetworkHost() 성공 후 콜백 구독 추가

```csharp
// 3. NetworkManager Host 시작
if (!StartNetworkHost())
{
    OnError?.Invoke("NetworkManager.StartHost() 실패.");
    return;
}

// [추가] Client 접속 콜백 구독 — HOST가 Client 연결을 감지해 LoadGameScene 트리거
NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
```

### 핸들러 메서드 추가

```csharp
/// <summary>
/// NGO Client 접속 콜백. HOST 전용.
/// Host 자신(LocalClientId)을 제외한 실제 Client 접속 시 OnClientConnected 발행.
/// </summary>
private void HandleClientConnected(ulong clientId)
{
    if (NetworkManager.Singleton == null) return;
    if (clientId == NetworkManager.Singleton.LocalClientId) return; // Host 자신 제외

    Debug.Log($"[Network] Client 접속 감지 (clientId={clientId}). OnClientConnected 발행.");
    OnClientConnected?.Invoke();
}
```

### `DisconnectAsync()` — 구독 해제 추가

```csharp
// 기존 ShutdownNetworkManager() 호출 전에 추가
if (NetworkManager.Singleton != null)
    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
```

---

## 수정 후 흐름

```
[Host]   HostGameAsync() → StartHost() → OnClientConnectedCallback 구독
         Client 접속 → HandleClientConnected() → OnClientConnected 발행
         BattleViewModel.OnClientConnected() → ConnectedPlayers=2 → LoadGameScene() ✅

[Client] JoinGameAsync() → OnClientConnected 발행
         BattleViewModel.OnClientConnected() → ConnectedPlayers=2 → LoadGameScene() 호출
         → IsServer=false → 무시 (정상 — Host가 이미 처리)
```
