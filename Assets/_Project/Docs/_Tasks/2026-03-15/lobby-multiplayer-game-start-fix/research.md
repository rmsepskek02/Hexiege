# Research: 멀티플레이 게임 시작 버그

**날짜:** 2026-03-15

---

## 문제 요약

Client가 "코드로 참가" 후 게임이 시작되지 않음.
`[Network] Client 게임 참가 완료.` 로그 이후 씬 전환 없음.

---

## 원인 분석

### BattleViewModel.OnClientConnected() 흐름

```csharp
private void OnClientConnected()
{
    ConnectedPlayers.Value++;
    if (ConnectedPlayers.Value >= 2)
        _networkManager.LoadGameScene();  // ← HOST만 호출 가능
}
```

### LoadGameScene()

```csharp
public void LoadGameScene()
{
    if (!NetworkManager.Singleton.IsServer) return;  // ← Client 측에서 호출 시 무시
    NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
}
```

### NetworkGameManager.OnClientConnected 발행 위치

```csharp
// JoinGameAsync() 내부 — Client 측에서만 발행
Debug.Log("[Network] Client 게임 참가 완료.");
OnClientConnected?.Invoke();
```

### 결론

| 측 | 상황 | 결과 |
|----|------|------|
| **HOST** | `HostGameAsync()` 완료 → `OnHostStarted` (ConnectedPlayers=1) → 클라이언트 접속 감지 수단 없음 | `LoadGameScene()` 미호출 |
| **CLIENT** | `JoinGameAsync()` 완료 → `OnClientConnected` 발행 → `LoadGameScene()` 호출 → `IsServer=false` → 무시 | `LoadGameScene()` 미호출 |

**NGO `NetworkManager.Singleton.OnClientConnectedCallback` 구독이 누락되어 HOST가 Client 접속을 감지하지 못함.**

---

## 영향 범위

- 수정 파일: `NetworkGameManager.cs` 단 1개
- 관련 클래스: `BattleViewModel` — 수정 불필요 (OnClientConnected 이벤트 구독 로직 유지)
