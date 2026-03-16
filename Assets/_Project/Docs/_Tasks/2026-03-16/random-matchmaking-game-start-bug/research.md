# Research: 랜덤 매칭 후 게임 화면 전환 안 되는 버그

**날짜:** 2026-03-16

---

## 증상

랜덤 매칭이 완료(MatchId 반환)되었는데 게임 화면(Game 씬)으로 전환되지 않음.

---

## 전체 매칭 → 게임 전환 흐름

```
[매칭 완료]
MatchmakerManager.PollUntilMatchedAsync() → matchId 반환
  ↓
MatchmakerManager.DetermineIsHostAsync(matchId) → bool isHost
  ↓
[Host]                              [Client]
HostGameAsync($"match_{matchId}")   JoinByMatchIdAsync(matchId)
  → CreateRelayAsync()               → 1초 간격 10회 Lobby 검색
  → CreateLobbyAsync(matchId 포함)   → JoinLobbyByIdAsync(lobbyId)
  → StartNetworkHost()               → GetRelayJoinCode()
  → OnClientConnectedCallback 등록   → JoinRelayAsync()
  → OnHostStarted 이벤트             → StartNetworkClient()
                                     → OnClientConnected 이벤트
  ↓
BattleViewModel.OnHostStarted()     BattleViewModel.OnClientConnected()
  → ConnectedPlayers = 1              → ConnectedPlayers++ (0→1)
                                      → 1 >= 2 FALSE → 게임 시작 안 함 (정상)
  ↓
[Client가 NGO에 접속하면]
NetworkGameManager.HandleClientConnected()
  → OnClientConnected 이벤트
  → BattleViewModel.OnClientConnected()
  → ConnectedPlayers++ (1→2)
  → LoadGameScene()
  ↓
NGO SceneManager.LoadScene("Game") → 양쪽 동기화
  ↓
NetworkGameFlow.OnNetworkSpawn()
  → WaitForTeamAndSendReady()
  → RequestReadyServerRpc()
  → 2명 준비 → StartGameClientRpc()
  → GameBootstrapper.StartNetworkGame(localTeam)
```

---

## 관련 파일 및 핵심 코드

### 1. MatchmakerManager.cs — DetermineIsHostAsync (Line 176~192)

```csharp
public async Task<bool> DetermineIsHostAsync(string matchId)
{
    var results = await MatchmakerService.Instance.GetMatchmakingResultsAsync(matchId);
    string playerId = AuthenticationService.Instance.PlayerId;

    List<Player> sortedPlayers = results.MatchProperties.Players
        .OrderBy(p => p.Id, StringComparer.Ordinal)
        .ToList();

    int hash = matchId.GetHashCode();           // ← ⚠️ 문제 지점
    int hostIndex = Math.Abs(hash) % sortedPlayers.Count;

    return sortedPlayers[hostIndex].Id == playerId;
}
```

### 2. NetworkGameManager.cs — HandleClientConnected 등록 순서 (Line 157~164)

```csharp
// 3. NetworkManager Host 시작
if (!StartNetworkHost()) { ... return; }

// 3-1. Client 접속 감지 콜백 구독 (Host 전용)
NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;  // ← ⚠️ StartHost 이후 등록
```

### 3. BattleViewModel.cs — OnClientConnected (Line 259~264)

```csharp
private void OnClientConnected()
{
    ConnectedPlayers.Value++;
    if (ConnectedPlayers.Value >= 2)
        _networkManager.LoadGameScene();
}
```

---

## 버그 원인 분석

### [Critical] Bug 1: `string.GetHashCode()` 크로스-프로세스 비결정성

**파일:** `MatchmakerManager.cs:188`

**원인:**
C#/.NET Core에서 `string.GetHashCode()`는 **프로세스 시작 시 무작위 시드**를 사용한다.
같은 문자열이라도 **서로 다른 기기(프로세스)에서 실행하면 다른 해시값을 반환**한다.

두 플레이어가 같은 `matchId`로 `DetermineIsHostAsync`를 호출하지만:
- Player A: `"match-abc".GetHashCode()` = 123456 → `hostIndex = 0` → Player A가 Host
- Player B: `"match-abc".GetHashCode()` = 789012 → `hostIndex = 1` → Player B도 Host

**결과 시나리오:**

| 시나리오 | 설명 | 증상 |
|---------|------|------|
| 둘 다 Host | 각자 Lobby 생성, NGO Host 시작. Client가 없으므로 `HandleClientConnected` 미발동. | 게임 화면 전환 안 됨 (무한 대기) |
| 둘 다 Client | Lobby 없음. `JoinByMatchIdAsync` 10회 실패. | 에러 메시지 "매칭된 방을 찾을 수 없습니다" |
| 올바른 분기 (운 좋을 때) | 정상 작동 | 게임 전환 성공 |

→ **확률적으로 절반은 "둘 다 Host", 절반은 "둘 다 Client"가 될 수 있음.**

**재현 조건:** 두 기기 또는 두 에디터 인스턴스에서 동시에 매칭할 때 발생. 단일 기기 테스트에서는 `.GetHashCode()` 결과가 같을 수 있어 간헐적으로 정상 작동처럼 보임.

---

### [Minor] Bug 2: `HandleClientConnected` 등록이 `StartNetworkHost()` 이후

**파일:** `NetworkGameManager.cs:157~164`

**원인:**
NGO `StartHost()` 호출 후 콜백을 등록하는 사이에 극히 짧은 시간 틈새가 존재한다.
이론상 이 틈새에 Client가 접속하면 `HandleClientConnected`가 발동하지 않는다.

**현실적 위험도:** 낮음 (Client는 Relay 참가 + NGO 핸드셰이크에 수 초가 걸림).
그러나 방어적으로 수정하는 것이 올바름.

---

## 영향 범위

| 파일 | 수정 필요 여부 |
|------|-------------|
| `MatchmakerManager.cs` | ✅ 필수 (Bug 1) |
| `NetworkGameManager.cs` | 권장 (Bug 2, 방어 코드) |
| 그 외 파일 | 수정 불필요 |

---

## 참고: 정상 흐름에서의 문제 없는 부분

- `BattleViewModel.OnClientConnected()` 로직 자체는 정상 (`ConnectedPlayers >= 2`이면 `LoadGameScene()`)
- `NetworkGameFlow.WaitForTeamAndSendReady()` 로직 정상 (Host=Blue, Client=Red)
- `LobbyManager.FindLobbyByMatchIdAsync()` 로직 정상 (matchId로 Lobby 검색 후 ID 반환)
- NGO SceneManager LoadScene 흐름 정상
