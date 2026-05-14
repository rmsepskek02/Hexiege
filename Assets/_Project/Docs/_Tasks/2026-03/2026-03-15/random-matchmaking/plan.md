# Plan: 랜덤 매칭 시스템

**날짜:** 2026-03-15

---

## 진행 순서

```
STEP 0 (수동) — UGS 대시보드 Queue/Pool 설정
STEP 1 (수동) — com.unity.services.matchmaker 패키지 설치
STEP 2 (코드) — MatchmakerManager.cs 신규 작성
STEP 3 (코드) — NetworkGameManager.cs 확장
STEP 4 (코드) — BattleViewModel.cs 확장
STEP 5 (코드) — RandomMatchView.cs 수정
```

---

## STEP 0 — UGS 대시보드 설정 (수동)

1. [dashboard.unity3d.com](https://dashboard.unity3d.com) 접속 → 프로젝트 선택
2. **Matchmaker** 탭 → Enable
3. **Queues** → Create Queue
   - Name: `hexiege-random`
   - Min Players: `2`, Max Players: `2`
   - Timeout: `0` (무제한)
4. Queue 내 **Pools** → Add Pool
   - Name: `default-pool`
   - Rule 없음 (순수 랜덤)

---

## STEP 1 — 패키지 설치 (수동)

Unity 에디터 → Window → Package Manager → `+` → Add package by name:
```
com.unity.services.multiplayer
```
> ⚠️ `com.unity.services.matchmaker`는 2025년 2월 deprecated. Matchmaker SDK는 `com.unity.services.multiplayer`에 통합됨.
> 네임스페이스(`Unity.Services.Matchmaker`)와 API(`MatchmakerService.Instance` 등)는 동일.

---

## STEP 2 — MatchmakerManager.cs (신규)

**경로**: `Assets/_Project/Scripts/Infrastructure/Network/MatchmakerManager.cs`

```csharp
// 역할: UGS Matchmaker SDK 래퍼
// - CreateTicketAsync(queueName) → ticketId 반환
// - PollUntilMatchedAsync(ticketId, onWaitSecond) → MatchmakingResults 반환
// - CancelTicketAsync(ticketId)

public class MatchmakerManager
{
    private const string QueueName = "hexiege-random";
    private const int PollIntervalMs = 2000; // 2초 간격 폴링

    // 티켓 생성
    public async Task<string> CreateTicketAsync()
    {
        var players = new List<Player>
        {
            new Player(AuthenticationService.Instance.PlayerId)
        };
        var options = new CreateTicketOptions { QueueName = QueueName };
        var response = await MatchmakerService.Instance.CreateTicketAsync(players, options);
        return response.Id;
    }

    // 매칭 완료까지 폴링. onWaitSecond: 1초마다 경과 시간 콜백
    public async Task<MatchmakingResults> PollUntilMatchedAsync(
        string ticketId,
        CancellationToken ct,
        Action<int> onWaitSecond = null)
    {
        int elapsed = 0;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(PollIntervalMs, ct);
            elapsed += PollIntervalMs / 1000;
            onWaitSecond?.Invoke(elapsed);

            var status = await MatchmakerService.Instance.GetTicketAsync(ticketId);
            if (status.Type == typeof(MatchmakingResults))
                return status.Value as MatchmakingResults;

            if (status.Type == typeof(MatchmakingFailed))
                throw new Exception("매칭 실패: " + (status.Value as MatchmakingFailed)?.Reason);
        }
        throw new OperationCanceledException();
    }

    // 티켓 취소
    public async Task CancelTicketAsync(string ticketId)
    {
        if (string.IsNullOrEmpty(ticketId)) return;
        await MatchmakerService.Instance.DeleteTicketAsync(ticketId);
    }

    // Host 결정: MatchId 해시 기반 결정론적 랜덤
    // 두 플레이어가 서버 통신 없이 동일한 결론 도출
    public bool DetermineIsHost(MatchmakingResults result)
    {
        int hash = result.MatchId.GetHashCode();
        var sortedPlayers = result.Players.OrderBy(p => p.Id).ToList();
        int hostIndex = Math.Abs(hash) % sortedPlayers.Count;
        return sortedPlayers[hostIndex].Id == AuthenticationService.Instance.PlayerId;
    }
}
```

---

## STEP 3 — NetworkGameManager.cs 확장

**추가 메서드 3개**:

```csharp
// ── 랜덤 매칭 ────────────────────────────────────────────────

private MatchmakerManager _matchmakerManager = new MatchmakerManager();
private string _currentTicketId;
private CancellationTokenSource _matchmakingCts;

/// <summary>
/// 랜덤 매칭 시작.
/// 매칭 완료 시 Host면 HostGameAsync, Client면 JoinByMatchIdAsync 호출.
/// onWaitSecond: 매초 경과 시간(초) 콜백 (UI 타이머용)
/// </summary>
public async Task StartMatchmakingAsync(Action<int> onWaitSecond = null)
{
    _matchmakingCts = new CancellationTokenSource();
    _currentTicketId = await _matchmakerManager.CreateTicketAsync();

    Debug.Log($"[Matchmaker] 티켓 생성: {_currentTicketId}");

    var result = await _matchmakerManager.PollUntilMatchedAsync(
        _currentTicketId, _matchmakingCts.Token, onWaitSecond);

    Debug.Log($"[Matchmaker] 매칭 완료. MatchId: {result.MatchId}");

    bool isHost = _matchmakerManager.DetermineIsHost(result);
    Debug.Log($"[Matchmaker] 역할 결정: {(isHost ? "Host" : "Client")}");

    if (isHost)
    {
        // 기존 HostGameAsync 재활용, Lobby 이름에 matchId 포함
        await HostGameAsync($"match_{result.MatchId}");
    }
    else
    {
        // Client는 matchId로 Lobby 검색 후 참가
        await JoinByMatchIdAsync(result.MatchId);
    }
}

/// <summary>
/// MatchId로 Lobby 검색 후 참가. Host가 생성한 Lobby를 찾을 때까지 폴링.
/// </summary>
private async Task JoinByMatchIdAsync(string matchId)
{
    const int maxRetries = 10;
    for (int i = 0; i < maxRetries; i++)
    {
        await Task.Delay(1000);
        string lobbyCode = await _lobbyManager.FindLobbyByMatchIdAsync(matchId);
        if (!string.IsNullOrEmpty(lobbyCode))
        {
            await JoinGameAsync(lobbyCode);
            return;
        }
        Debug.Log($"[Matchmaker] Lobby 대기 중... ({i + 1}/{maxRetries})");
    }
    OnError?.Invoke("매칭된 방을 찾을 수 없습니다. 다시 시도해주세요.");
}

/// <summary>
/// 랜덤 매칭 취소. 티켓 삭제 후 CancellationToken 취소.
/// </summary>
public async Task CancelMatchmakingAsync()
{
    _matchmakingCts?.Cancel();
    await _matchmakerManager.CancelTicketAsync(_currentTicketId);
    _currentTicketId = null;
    Debug.Log("[Matchmaker] 매칭 취소 완료.");
}
```

---

## STEP 3-1 — LobbyManager.cs 확장

`FindLobbyByMatchIdAsync(matchId)` 메서드 추가:

```csharp
// matchId를 Lobby 이름으로 검색
public async Task<string> FindLobbyByMatchIdAsync(string matchId)
{
    var queryOptions = new QueryLobbiesOptions
    {
        Filters = new List<QueryFilter>
        {
            new QueryFilter(
                field: QueryFilter.FieldOptions.Name,
                value: $"match_{matchId}",
                op: QueryFilter.OpOptions.EQ)
        }
    };
    var results = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
    return results.Results.FirstOrDefault()?.LobbyCode;
}
```

---

## STEP 4 — BattleViewModel.cs 확장

### 추가 상태
```csharp
/// <summary>매칭 대기 시간(초). UI 타이머 표시용.</summary>
public ReactiveProperty<int> MatchWaitSeconds = new(0);
```

### CmdStartMatchmaking 처리 추가
```csharp
CmdStartMatchmaking
    .Subscribe(async _ =>
    {
        try
        {
            CurrentScreen.Value = BattleScreen.RandomMatch;
            IsMatchmaking.Value = true;
            MatchWaitSeconds.Value = 0;

            await _networkManager.StartMatchmakingAsync(
                onWaitSecond: sec => MatchWaitSeconds.Value = sec);
        }
        catch (OperationCanceledException)
        {
            IsMatchmaking.Value = false;
            CurrentScreen.Value = BattleScreen.Main;
        }
        catch (Exception e)
        {
            ErrorMessage.Value = e.Message;
            IsMatchmaking.Value = false;
        }
    })
    .AddTo(_disposables);
```

### CmdCancelMatchmaking 처리 추가
```csharp
CmdCancelMatchmaking
    .Subscribe(async _ =>
    {
        try { await _networkManager.CancelMatchmakingAsync(); }
        catch { }
        IsMatchmaking.Value = false;
        CurrentScreen.Value = BattleScreen.Main;
    })
    .AddTo(_disposables);
```

---

## STEP 5 — RandomMatchView.cs 수정

### MatchWaitSeconds 구독 추가
```csharp
// 대기 시간 포맷: "매칭 중... 00:13"
vm.MatchWaitSeconds
    .Subscribe(sec =>
    {
        if (_statusText != null)
        {
            int min = sec / 60;
            int s = sec % 60;
            _statusText.text = $"매칭 중... {min:00}:{s:00}";
        }
    })
    .AddTo(_disposables);
```

### "랜덤 매칭 시작" 버튼 연결
RandomMatchView에 "시작" 버튼 추가 또는 화면 진입 시 자동 시작:
```csharp
// 화면 활성화 시 자동 매칭 시작 (BattleMainView에서 이미 CmdStartMatchmaking 호출)
vm.IsMatchmaking
    .Subscribe(matching =>
    {
        if (_cancelButton != null) _cancelButton.gameObject.SetActive(matching);
    })
    .AddTo(_disposables);
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| Host Lobby 생성 전 Client가 검색 | 최대 10초 재시도 (1초 간격) |
| 매칭 중 앱 종료 | OnApplicationQuit에서 CancelMatchmakingAsync 호출 |
| UGS 대시보드 Queue 이름 불일치 | `MatchmakerManager.QueueName` 상수와 대시보드 동일해야 함 |
| MatchmakingResults vs MultiplayAssignment | 전용 서버 없이 Client-only 모드이므로 MatchmakingResults 타입 사용 |

---

## 테스트 체크리스트

- [ ] STEP 0: UGS 대시보드 Queue `hexiege-random` 생성 확인
- [ ] STEP 1: `com.unity.services.matchmaker` 설치 확인
- [ ] 두 기기에서 "랜덤 매칭" 버튼 → 매칭 대기 화면 진입
- [ ] 대기 시간 타이머 1초마다 증가 확인
- [ ] 매칭 완료 → 한 쪽 Host / 다른 쪽 Client 역할 정확히 분리
- [ ] Game 씬 정상 진입, 기존 멀티플레이 흐름 유지
- [ ] "취소" 버튼 → 매칭 취소 → Main 화면 복귀
- [ ] Host/Client 역할이 여러 번 테스트에서 50:50으로 랜덤하게 결정됨
