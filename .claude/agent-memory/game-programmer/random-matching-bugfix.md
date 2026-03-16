---
name: random-matching-bugfix
description: 2026-03-16 랜덤 매칭 Host/Client 결정 GetHashCode() 크로스-프로세스 버그 수정
type: project
---

# 랜덤 매칭 Host/Client 결정 버그 수정 (2026-03-16)

## 핵심 원인
- `MatchmakerManager.DetermineIsHostAsync()`에서 `matchId.GetHashCode()` 사용
- .NET Core string.GetHashCode()는 프로세스별 무작위 시드 → 같은 문자열이라도 다른 기기에서 다른 값
- 결과: 두 플레이어가 모두 Host 또는 모두 Client → 게임 씬 전환 불가

## 수정 (MatchmakerManager.cs)
```csharp
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

// DetermineIsHostAsync
int hostIndex = GetStableHash(matchId) % sortedPlayers.Count;
```
- MatchId = UGS UUID → 매치마다 다른 해시 → 50/50 분배

## 수정 (NetworkGameManager.cs)
- `HostGameAsync()` 내 `OnClientConnectedCallback` 등록을 `StartNetworkHost()` 이전으로 이동
- `StartNetworkHost()` 실패 시 콜백 명시적 해제 (`-= HandleClientConnected`)
