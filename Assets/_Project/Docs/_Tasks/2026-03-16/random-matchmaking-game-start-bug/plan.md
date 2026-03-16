# Plan: 랜덤 매칭 후 게임 화면 전환 안 되는 버그 수정

**날짜:** 2026-03-16

---

## 수정 목표

1. **[Critical]** `DetermineIsHostAsync` — `GetHashCode()` 비결정성 제거 → MatchId 안정 해시 방식으로 교체
2. **[Minor]** `HandleClientConnected` 등록 순서를 `StartNetworkHost()` 이전으로 이동

---

## 수정 파일

| 파일 | 수정 내용 |
|------|---------|
| `Assets/_Project/Scripts/Infrastructure/Network/MatchmakerManager.cs` | `GetHashCode()` → 안정 해시(`GetStableHash`) + MatchId에 적용 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs` | 콜백 등록 순서 변경 |

---

## 수정 1: MatchmakerManager.cs — DetermineIsHostAsync

### 현재 코드 (Line 176~192)

```csharp
public async Task<bool> DetermineIsHostAsync(string matchId)
{
    var results = await MatchmakerService.Instance.GetMatchmakingResultsAsync(matchId);
    string playerId = AuthenticationService.Instance.PlayerId;

    List<Player> sortedPlayers = results.MatchProperties.Players
        .OrderBy(p => p.Id, StringComparer.Ordinal)
        .ToList();

    int hash = matchId.GetHashCode();                     // ← 삭제
    int hostIndex = Math.Abs(hash) % sortedPlayers.Count; // ← 삭제

    return sortedPlayers[hostIndex].Id == playerId;
}
```

### 수정 코드

```csharp
/// <summary>
/// 크로스-플랫폼/크로스-프로세스 결정론적 해시.
/// .NET string.GetHashCode()는 프로세스마다 다른 시드를 사용하므로 직접 구현.
/// </summary>
private static int GetStableHash(string s)
{
    unchecked
    {
        int hash = 17;
        foreach (char c in s)
            hash = hash * 31 + c;
        return Math.Abs(hash);
    }
}

public async Task<bool> DetermineIsHostAsync(string matchId)
{
    var results = await MatchmakerService.Instance.GetMatchmakingResultsAsync(matchId);
    string playerId = AuthenticationService.Instance.PlayerId;

    // 플레이어 ID를 사전순(Ordinal) 정렬 — 결정론적, 크로스-플랫폼 일관
    List<Player> sortedPlayers = results.MatchProperties.Players
        .OrderBy(p => p.Id, StringComparer.Ordinal)
        .ToList();

    if (sortedPlayers.Count == 0) return false;

    // MatchId(UGS 발급 UUID) 안정 해시로 인덱스 결정
    // → 매 매치마다 다른 UUID → 50/50 무작위 분배 보장
    // → 크로스-플랫폼/프로세스에서 동일한 결과 보장
    int hostIndex = GetStableHash(matchId) % sortedPlayers.Count;
    return sortedPlayers[hostIndex].Id == playerId;
}
```

**변경 요약:**
- `matchId.GetHashCode()` 제거 → `GetStableHash(matchId)` 로 교체
- `GetStableHash()`: polynomial hash (seed=17, multiplier=31) — 모든 플랫폼/프로세스에서 동일한 결과 보장
- MatchId는 UGS가 발급하는 랜덤 UUID → 매 매치마다 다른 해시 → 50/50 무작위 Host 분배
- `sortedPlayers.Count > 0` 방어 처리 추가 (DivideByZero 예방)

---

## 수정 2: NetworkGameManager.cs — HandleClientConnected 등록 순서

### 현재 코드 (Line 156~167)

```csharp
// 3. NetworkManager Host 시작
if (!StartNetworkHost())
{
    OnError?.Invoke("NetworkManager.StartHost() 실패.");
    return;
}

// 3-1. Client 접속 감지 콜백 구독 (Host 전용)
NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

// 4. Host Heartbeat 시작
StartHeartbeat();
```

### 수정 코드

```csharp
// 3-1. Client 접속 감지 콜백 구독 — StartHost() 이전에 등록 (레이스 컨디션 방지)
NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

// 3. NetworkManager Host 시작
if (!StartNetworkHost())
{
    // 실패 시 등록한 콜백 해제 후 에러 반환
    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
    OnError?.Invoke("NetworkManager.StartHost() 실패.");
    return;
}

// 4. Host Heartbeat 시작
StartHeartbeat();
```

**변경 요약:**
- 콜백 등록을 `StartNetworkHost()` 이전으로 이동
- `StartNetworkHost()` 실패 시 등록한 콜백을 명시적으로 해제

---

## 위험 요소

| 위험 | 평가 | 대응 |
|------|------|------|
| `GetMatchmakingResultsAsync` API 미지원 | 낮음 (이미 구현 중이므로 사용 중으로 파악) | 에러 발생 시 `catch (Exception e)`에서 `ErrorMessage` 표시됨 |
| MatchId UUID 충돌로 동일 인덱스 가능성 | 무시 가능 (UUID 공간 크기상 사실상 불가) | - |
| 수정 2 콜백 등록 시점 변경으로 Host 자신(clientId == LocalClientId) 이벤트 발동 가능성 | 없음 | `HandleClientConnected`에 이미 `if (clientId == LocalClientId) return` 필터 존재 |

---

## 테스트 체크리스트

- [x] 두 기기에서 동시에 "랜덤 매칭" 버튼 → 매칭 완료
- [x] 한 쪽은 Host, 다른 쪽은 Client로 역할이 다르게 결정되는지 확인
- [x] Game 씬으로 양쪽 모두 정상 전환되는지 확인
- [ ] 여러 번 반복 매칭 테스트 — Host/Client 역할이 번갈아 바뀌는지 확인 (무작위성 검증)
- [ ] 매칭 취소 후 재매칭 시에도 정상 동작 확인

---

## 완료 기록

**구현 완료:** 2026-03-16
**테스트 결과:** 게임 씬 정상 진입 확인. 반복 매칭 및 취소 후 재매칭 테스트 미완료.
