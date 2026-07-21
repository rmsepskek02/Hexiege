# Research — 매치메이킹 404 (호스트 결정 단계 실패)

## 이 작업은 무엇이고 왜 하는가 (자연어 설명)

지금 게임에서 "랜덤 매칭"을 눌러 상대를 찾는 과정은 **매칭 자체까지는 정상적으로 성공**합니다.
두 플레이어가 같은 매치(MatchId)로 묶이는 것까지는 잘 되는데, 바로 그 다음 단계인
**"둘 중 누가 방장(호스트)이 될지 정하는 단계"에서 서버 404(Not Found) 오류가 나면서 연결이 끊깁니다.**

원인을 코드와 오류 스택, Unity 공식 문서로 추적한 결과가 명확합니다.
현재 코드는 호스트를 정하려고 "매치 결과 조회 API(`GetMatchmakingResultsAsync`)"를 호출하는데,
**이 API는 전용 서버(Multiplay) 환경에서 서버가 매치 결과를 조회할 때 쓰라고 만들어진 API**입니다.
우리 게임은 전용 서버가 아니라 **플레이어끼리 직접 연결하는 P2P(Relay) 방식**이라서,
일반 플레이어가 이 API를 호출하면 조회할 리소스가 없어 404가 반환됩니다.

즉, "매칭이 안 되는 문제"가 아니라 **"매칭 후 호스트를 정하는 방법을 잘못 골라서 생기는 문제"**입니다.
이 문서는 그 오류가 정확히 어디서, 왜 발생하는지 코드 기준으로 정리합니다.
해결 방법(설계)은 별도의 `Plan.md`에 기술합니다.

> ⚠️ 이 문서는 현황 파악용입니다. 코드는 수정하지 않았으며, 실제 구현은 사용자 승인 후 `game-programmer` 에이전트가 진행합니다.

---

## 1. 증상 (사용자가 겪는 현상)

- 랜덤 매칭 시도 → 매칭은 잡히는 것처럼 보이나 게임으로 연결되지 않음.
- 콘솔 로그:
  ```
  [Matchmaker] StartMatchmakingAsync 예외: HTTP/1.1 404 Not Found
  ```
  이 로그는 `NetworkGameManager.cs:411`의 `catch (Exception e)` 블록에서 출력됨.
- 예외 스택에 `AsyncTaskMethodBuilder<bool>:SetException`이 나타남 → 반환형이 `Task<bool>`인 비동기 메서드에서 예외가 던져졌음을 의미. 프로젝트에서 `Task<bool>`을 반환하며 네트워크를 호출하는 지점은 `DetermineIsHostAsync` 하나뿐.

---

## 2. 404의 정확한 발원지 (코드 기준 확정)

### 발생 지점
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/MatchmakerManager.cs`
- **메서드**: `DetermineIsHostAsync(string matchId)` — 176~194행
- **직접 원인 호출**: 179행
  ```csharp
  var results = await MatchmakerService.Instance.GetMatchmakingResultsAsync(matchId);
  ```

### catch 지점
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs`
- 391행에서 `bool isHost = await _matchmakerManager.DetermineIsHostAsync(matchId);` 호출.
- 409~412행 `catch (Exception e)`가 404 예외를 잡아 `Debug.LogError`만 수행 → 이후 흐름(HostGame/Join) 진입 못 함.

### 근본 원인
- `GetMatchmakingResultsAsync`는 **전용 서버(Multiplay)가 매치 결과(플레이어/팀 분배)를 조회**하기 위한 서버 지향 API.
- Hexiege는 **P2P(Relay) + MatchIdAssignment** 구조. 폴링 응답으로 받는 것은 `MatchIdAssignment`(내용은 MatchId 하나)뿐이며, 이는 모든 플레이어가 공통으로 받는 값.
- P2P 클라이언트가 `GetMatchmakingResultsAsync(matchId)`를 호출하면 조회 대상 결과 리소스가 존재하지 않아 **404 Not Found**가 반환됨.
- 매칭 자체(티켓 생성 → 폴링 → MatchId 발급)는 성공하며, **오직 그 직후 "호스트 결정" 단계에서만 실패**함.

---

## 3. 현재 코드 흐름

### 3-1. `NetworkGameManager.StartMatchmakingAsync` (373~413행)

1. `_matchmakerManager.CreateTicketAsync()` → 티켓 생성 (380행) — **정상**
2. `_matchmakerManager.PollUntilMatchedAsync(...)` → matchId 획득 (383행) — **정상**
3. `onMatchFound?.Invoke()` → 매칭 성사 콜백 (389행) — **정상**
4. `bool isHost = await _matchmakerManager.DetermineIsHostAsync(matchId)` (391행) — **← 여기서 404**
5. 분기 (394~403행):
   - `isHost == true` → `HostGameAsync($"match_{matchId}", matchId)` (397행)
   - `isHost == false` → `JoinByMatchIdAsync(matchId)` (402행)

> 4번에서 예외가 발생하므로 5번 분기에 **도달하지 못함**.

### 3-2. `MatchmakerManager` (MatchmakerManager.cs)

- 상수: `QueueName = "hexiege-random"` (43행), `PollIntervalMs = 1000` (46행)
- `LastMatchId` 프로퍼티 (53행)
- `CreateTicketAsync()` (63행) — 티켓 생성
- `PollUntilMatchedAsync(...)` (86행) — `MatchIdAssignment.Status == Found`이면 matchId 반환 (108~110행)
- `CancelTicketAsync(...)` (160행)
- `DetermineIsHostAsync(matchId)` (176~194행):
  - 179행: `GetMatchmakingResultsAsync(matchId)` 호출 (**404 발원지**)
  - 183~185행: `results.MatchProperties.Players`를 Id 사전순(Ordinal) 정렬
  - 192행: `GetStableHash(matchId) % sortedPlayers.Count`로 hostIndex 결정
  - 193행: `sortedPlayers[hostIndex].Id == playerId` 반환
- `GetStableHash(string)` (200~209행) — 크로스-프로세스 결정론적 해시

### 3-3. `JoinByMatchIdAsync` (NetworkGameManager.cs:420~438)

- 최대 10회(1초 간격) 폴링하며 `_lobbyManager.FindLobbyByMatchIdAsync(matchId)` 호출.
- lobbyId를 찾으면 `JoinGameByIdAsync(lobbyId)`로 참가 (446행 이하: Lobby 참가 → RelayJoinCode 획득 → Relay 참가 → StartClient).
- 10회 내 못 찾으면 `OnError?.Invoke("매칭된 방을 찾을 수 없습니다...")`.

### 3-4. `LobbyManager` (LobbyManager.cs) — 현재 제공 메서드

- 상수: `RelayJoinCodeKey = "RelayJoinCode"` (41행), `MatchIdKey = "MatchId"` (44행)
- `CurrentLobby` 프로퍼티 (54행)
- `IsHost` 프로퍼티 (57~59행): `CurrentLobby != null && CurrentLobby.HostId == AuthenticationService.Instance.PlayerId`
- `CreateLobbyAsync(lobbyName, maxPlayers=2, relayJoinCode=null, matchId=null)` (73행):
  - matchId가 있으면 `MatchIdKey`를 **S1 인덱스 필드**로 Lobby Data에 저장 (91~99행)
- `GetLobbiesAsync(...)` (126행): 빈 슬롯 있는 공개 로비 조회
- `FindLobbyByMatchIdAsync(matchId)` (164행): 전체 로비 조회 후 클라이언트에서 `MatchIdKey` 값으로 필터링 → lobbyId 반환 (S1 인덱스 전파 지연 우회 목적, 169행 주석)
- `JoinLobbyByIdAsync(lobbyId)` (221행)
- `UpdateRelayJoinCodeAsync(relayJoinCode)` (284행): 기존 Lobby Data에 RelayJoinCode 갱신
- **주의**: 현재는 QueryLobbies 기반의 **비원자적** 검색/생성만 있음. 서버측에서 "없으면 생성, 있으면 참가"를 한 번에 처리하는 **CreateOrJoin 원자적 메서드는 없음**.

---

## 4. 왜 기존 해시 방식은 살릴 수 없는가

`DetermineIsHostAsync`의 해시 방식은 **전체 플레이어 목록**을 matchId 해시로 정렬해 호스트를 뽑는다.
그러나:

- P2P 클라이언트는 자기 `PlayerId`만 알고 **상대 `PlayerId`는 모른다.**
- 그 "전체 플레이어 목록"을 제공하던 유일한 소스가 바로 **404를 내는 `GetMatchmakingResultsAsync`**다.
- 따라서 이 API를 제거하면 **해시 방식 자체가 입력 데이터를 잃어 성립 불가**하다.

→ 호스트 결정 방식 자체를 바꿔야 하며, 단순히 API만 교체하는 문제가 아니다.

---

## 5. Unity 공식 P2P(Matchmaker + Relay) 표준 흐름 (참고 근거)

1. 각 플레이어는 폴링으로 `MatchIdAssignment`(내용은 MatchId 하나, **모든 플레이어 공통**)를 받는다.
2. 그 MatchId를 키로 **Lobby에 CreateOrJoin(없으면 생성 / 있으면 참가)** 요청을 보낸다.
3. **먼저 Lobby를 만든 쪽이 호스트**가 되어 Relay를 할당하고 heartbeat를 유지한다.
4. 나중에 온 쪽은 자동으로 그 Lobby에 참가(클라이언트)한다.

즉, **P2P 정석의 호스트 결정은 "Lobby CreateOrJoin 선점"이지 매치 결과 조회가 아니다.**
CreateOrJoin은 서버측에서 원자적으로 처리되므로, 두 클라이언트가 동시에 시도해도 **정확히 한 명만 호스트**가 되어 race condition이 원천 차단된다.

---

## 6. 영향 범위

| 파일 | 영향 |
|------|------|
| `MatchmakerManager.cs` | `DetermineIsHostAsync` + `GetMatchmakingResultsAsync` 호출이 호스트 결정 경로에서 제거/비활성화 대상. `GetStableHash`는 이 메서드 전용이므로 함께 사용처 확인 필요 |
| `NetworkGameManager.cs` | `StartMatchmakingAsync`의 391~403행(호스트 결정 + 분기) 흐름 재구성 필요. `JoinByMatchIdAsync` 폴링과의 정합성 검토 필요 |
| `LobbyManager.cs` | CreateOrJoin 원자적 래퍼 신규 추가 필요. 기존 `IsHost`, `UpdateRelayJoinCodeAsync`, `MatchIdKey`, `RelayJoinCodeKey` 재사용 가능 |

---

## 7. 아키텍처 제약 확인 (.claude/MEMORY.md 기준)

- 위 3개 파일 모두 **Infrastructure 레이어**(`Hexiege.Infrastructure`)에 위치 → NetworkBehaviour/네트워크 SDK 직접 참조가 허용되는 유일한 레이어. **제약 위반 없음.**
- 멀티플레이 패키지는 `com.unity.services.multiplayer` 2.0.0 (Matchmaker/Lobby/Relay 통합 SDK).
- `GameSystemRules`에는 **매치메이킹/네트워크 전용 규칙 파일이 존재하지 않음** → 이 작업의 설계 근거는 위 아키텍처 제약 + Unity 공식 P2P 흐름을 따른다 (Plan.md에서 상술).

---

## 8. 미확인/후속 검증 필요 항목 (정직한 기록)

- `com.unity.services.multiplayer@2.0.0`에서 **Lobby CreateOrJoin의 정확한 API 시그니처**(예: `LobbyService.Instance.CreateOrJoinLobbyAsync`의 파라미터 형태, matchId를 lobbyId로 직접 사용 가능한지 여부)는 이 조사 시점에 SDK 소스로 확정하지 못함.
  → **구현 단계에서 `game-programmer`가 설치된 SDK 버전 기준으로 최종 확인**해야 함. (Plan.md 위험 요소에 명시)
