# Plan — 매치메이킹 404 해결: 호스트 결정을 Lobby CreateOrJoin으로 전환

## 이 작업은 무엇이고 왜 하는가 (자연어 설명)

랜덤 매칭 후 **"누가 방장(호스트)이 될지 정하는 단계"에서 404 오류가 나서 게임 연결이 끊기는 문제**를 해결합니다.

지금은 호스트를 정하려고 "매치 결과 조회 API"를 쓰는데, 이 API는 우리 게임처럼
플레이어끼리 직접 붙는 P2P(Relay) 방식에는 맞지 않아 404를 냅니다 (상세는 `Research.md` 참조).

해결 방향은 Unity 공식 P2P 표준을 그대로 따르는 것입니다.
**"매치 결과를 조회해서 호스트를 계산"하는 대신, 두 플레이어 모두 같은 MatchId를 키로 방(Lobby)에 CreateOrJoin(없으면 만들고 / 있으면 참가)을 요청**합니다.
이러면 **먼저 방을 만든 사람이 자동으로 호스트**가 됩니다. 이 처리는 서버가 한 번에(원자적으로) 해주기 때문에,
두 사람이 동시에 눌러도 **정확히 한 명만 호스트가 되어** 충돌(race condition)이 원천적으로 사라집니다.

쉽게 말해 **"호스트를 계산으로 뽑는 방식"에서 "먼저 방을 선점한 사람이 호스트가 되는 방식"으로 바꾸는 것**입니다.

> ⚠️ 이 문서는 계획이며, 사용자 명시적 승인 전까지 코드는 수정하지 않습니다.
> 실제 구현은 승인 후 `game-programmer` 에이전트가 진행합니다 (CLAUDE.md 규칙 3, 11).

---

## ⚠️ 기존 로직 제거 규칙 (WORKFLOW [4] — 문서 최상단 명시)

아래 기존 로직은 이 작업으로 **호스트 결정 경로에서 빠집니다.**

- `MatchmakerManager.DetermineIsHostAsync` (176~194행)
- 그 내부의 `GetMatchmakingResultsAsync` 호출 (179행) — **404의 직접 원인**
- 이 메서드 전용으로만 쓰이는 `GetStableHash` (200~209행) — 사용처가 여기뿐임을 구현 시 재확인 후 처리

**제거 방식 (예외 없음):**
- 검증 전까지는 **"즉시 삭제"가 아니라 "비활성화(주석 처리)"를 기본**으로 한다.
- 주석 처리된 로직의 **최종 삭제는 [6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전** 단계에서 수행한다.

**제거해도 안전한 근거:**
- 이 로직의 입력 데이터(전체 플레이어 목록)를 주던 소스가 곧 404를 내는 `GetMatchmakingResultsAsync`이므로, 해당 API를 빼면 해시 방식은 입력을 잃어 성립 불가하다 (Research.md §4).
- 새 방식(Lobby CreateOrJoin 선점)이 호스트 결정 기능을 완전히 대체한다.

---

## 채택 해결책 — A방식: Lobby CreateOrJoin 원자적 선점

Unity 공식 P2P 정석대로, 호스트 결정을 **매치 결과 조회가 아니라 Lobby CreateOrJoin으로 전환**한다.

- 모든 플레이어가 `matchId`를 키로 Lobby에 CreateOrJoin 요청.
  - Lobby가 없으면 **생성 → 그 플레이어가 호스트**.
  - Lobby가 있으면 **참가 → 클라이언트**.
- 서버측 원자적 처리로 두 클라이언트 동시 시도에도 정확히 한 명만 호스트가 됨 → race condition 원천 차단.
- 호스트/클라이언트 판별은 기존 `LobbyManager.IsHost`(`CurrentLobby.HostId == 내 PlayerId`, 57~59행)를 **재사용**.

---

## 수정 대상 파일 및 변경 요지

### 1. `Assets/_Project/Scripts/Infrastructure/Network/LobbyManager.cs`
- **[추가]** CreateOrJoin 원자적 래퍼 메서드 신규 (가칭 `CreateOrJoinLobbyByMatchIdAsync(matchId, ...)`).
  - matchId를 키로 "없으면 생성 / 있으면 참가"를 한 번에 처리.
  - 생성 시 기존 `CreateLobbyAsync`와 동일하게 `MatchIdKey`(44행) 저장 규칙을 따르고, RelayJoinCode는 이후 `UpdateRelayJoinCodeAsync`(284행)로 채우는 기존 방식을 유지.
  - 반환 결과와 `IsHost` 프로퍼티로 호출측이 호스트 여부 판별 가능하게 함.
- **[재사용]** `IsHost`(57행), `UpdateRelayJoinCodeAsync`(284행), 상수 `MatchIdKey`/`RelayJoinCodeKey`(41·44행).
- **[검토]** 기존 `FindLobbyByMatchIdAsync`(164행)의 존치 여부 — `JoinByMatchIdAsync` 폴링과의 정합성에 따라 결정 (아래 위험 요소 참조).

### 2. `Assets/_Project/Scripts/Infrastructure/Network/MatchmakerManager.cs`
- **[비활성화]** `DetermineIsHostAsync`(176~194행) 전체를 주석 처리 (즉시 삭제 아님 — 위 제거 규칙).
- **[비활성화 검토]** `GetStableHash`(200~209행) — 다른 사용처가 없는지 확인 후 함께 주석 처리.
- `CreateTicketAsync`/`PollUntilMatchedAsync`/`CancelTicketAsync`는 **변경 없음** (매칭 자체는 정상).

### 3. `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs`
- **[수정]** `StartMatchmakingAsync`(373~413행)의 호스트 결정 + 분기 흐름 재구성:
  - 391행 `DetermineIsHostAsync` 호출 제거.
  - 매칭 성사(`onMatchFound`) 후, 모든 플레이어가 `LobbyManager.CreateOrJoinLobbyByMatchIdAsync(matchId)` 호출.
  - `IsHost == true`이면: Relay 할당 → `UpdateRelayJoinCodeAsync`로 JoinCode 공유 → StartHost 흐름 (기존 `HostGameAsync`의 Relay·Host 로직 재활용 범위는 구현 시 확정).
  - `IsHost == false`이면: Lobby의 RelayJoinCode가 채워질 때까지 대기 → Relay 참가 → StartClient (기존 `JoinGameByIdAsync` 440~465행의 참가 로직 재활용).
- **[검토]** 기존 `JoinByMatchIdAsync`(420~438행) 폴링 로직 — CreateOrJoin 도입 후 클라이언트 참가 경로가 어떻게 통합되는지에 따라 존치/수정/제거 결정.

> 각 파일별 최종 라인 단위 변경은 구현 담당(`game-programmer`)이 SDK 시그니처 확인 후 확정한다.

---

## 아키텍처 근거 (.claude/MEMORY.md 제약 부합 확인)

- 수정 대상 3개 파일 모두 **Infrastructure 레이어**(`Hexiege.Infrastructure`)에 위치. 네트워크 SDK(`com.unity.services.multiplayer` 2.0.0) 및 NetworkBehaviour 참조가 허용되는 유일한 레이어 → **"NetworkBehaviour는 Infrastructure에만" 제약 부합.**
- 본 작업은 Infrastructure 내부 로직 교체이며 **Application → Unity.Netcode 직접 참조**나 **Application → Infrastructure 역참조**를 새로 만들지 않음 → 의존성 방향 제약 부합.
- 의존성 조합 루트(`GameBootstrapper`)나 NGO RPC 네이밍(`ServerRpc`/`ClientRpc`), Enable Scene Management 설정은 이 작업 범위에서 변경 없음.
- **GameSystemRules 근거**: 인덱스(`GameSystemRules.md`) 및 하위 파일 확인 결과 **매치메이킹/네트워크 전용 규칙 파일이 존재하지 않음.** 따라서 이 작업의 설계 근거는 **① .claude/MEMORY.md 아키텍처 제약 + ② Unity 공식 Matchmaker+Relay(P2P) 표준 흐름**을 기준으로 한다 (Research.md §5).

---

## 위험 요소 및 후속 검증 필요 항목

1. **[미검증 — 구현 전 필수 확인] CreateOrJoin API 시그니처**
   - `com.unity.services.multiplayer@2.0.0`에서 Lobby CreateOrJoin의 정확한 API 형태(예: `LobbyService.Instance.CreateOrJoinLobbyAsync`의 파라미터, matchId를 lobbyId로 직접 쓸 수 있는지, 아니면 별도 키 매핑이 필요한지)를 이 계획 시점에 SDK 소스로 확정하지 못함.
   - → **`game-programmer`가 설치된 SDK 버전 기준으로 최종 확인 후 구현**해야 함. 시그니처가 예상과 다르면 LobbyManager 래퍼 설계를 조정한다.

2. **Relay JoinCode 공유 타이밍**
   - 호스트가 CreateOrJoin으로 Lobby를 만든 직후 Relay를 할당하고 `UpdateRelayJoinCodeAsync`로 Lobby Data에 JoinCode를 기록하기까지 **시간차**가 존재.
   - 클라이언트는 Lobby에 참가한 뒤 **RelayJoinCode가 채워질 때까지 대기(폴링)**해야 함. 기존 `JoinGameByIdAsync`가 JoinCode 없으면 즉시 에러 처리(450행)하므로, 이 대기 로직을 반드시 반영.

3. **기존 `JoinByMatchIdAsync` 폴링과의 정합성**
   - CreateOrJoin 도입 시 "Lobby를 찾아 참가"하는 별도 폴링(`FindLobbyByMatchIdAsync` 기반, 420~438행)이 중복되거나 충돌할 수 있음.
   - 클라이언트 참가 경로를 CreateOrJoin 한 곳으로 일원화할지, 기존 폴링을 JoinCode 대기용으로만 남길지 구현 시 결정.

4. **동시 생성 극단 케이스 재확인**
   - CreateOrJoin이 서버측 원자성을 보장한다는 전제이나, SDK 2.0.0의 실제 동작(동시 요청 시 한쪽이 참가로 귀결되는지)을 구현 후 멀티 실기(Host+Client)로 검증 필요.

---

## 예상 변경 파일 목록 (최종은 [13]에서 확정)

```
[수정]
- Assets/_Project/Scripts/Infrastructure/Network/LobbyManager.cs
- Assets/_Project/Scripts/Infrastructure/Network/MatchmakerManager.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs

[작업 문서]
- Assets/_Project/Docs/_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/Research.md
- Assets/_Project/Docs/_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/Plan.md
```

---

## 실제 구현 결과 (2026-07-17 추가 — 계획 대비 확정/변경 기록)

> 이 절은 위 계획을 실제로 구현한 뒤, 계획과 달라졌거나 계획 시점에 미확정이던 부분을 사실 기준으로 기록한 것입니다. Research/Plan 본문은 히스토리 보존을 위해 원래대로 두고, 확정 내용만 여기에 덧붙입니다.
>
> **진행 상태(정확히 이대로):** A방식 구현을 완료하여 브랜치 `claude/matchmaker-404-error-pi9qdn` 커밋 `a3dbc73`으로 푸시했다. **초기 매칭 실기에서 404 없이 정상 연결되는 것을 확인**했으나, 이 버그는 **간헐적(intermittent)** 이라 사용자가 **지속 테스트 중**이다. 따라서 "완전 검증 PASS"가 아니라 **"초기 정상, 지속 관찰 중"** 상태이며, 비활성화(주석 처리)한 레거시 코드는 **아직 최종 삭제하지 않고 유지**한다(지속 테스트 확정 후 별도 단계에서 삭제).

### 계획대로 구현된 항목
- **호스트 결정 방식 전환(A방식)**: 매치 결과 조회(404 원인)를 폐기하고, 모든 플레이어가 같은 `matchId`를 키로 Lobby CreateOrJoin 선점 → 먼저 만든 쪽이 호스트. 계획과 동일.
- **`LobbyManager.CreateOrJoinLobbyByMatchIdAsync(matchId, lobbyName, maxPlayers = 2)` 신규**: 계획대로 추가. 생성 시에만 `MatchIdKey`를 S1 인덱스로 저장(기존 `CreateLobbyAsync` 저장 규칙 준수), RelayJoinCode는 호스트가 이후 `UpdateRelayJoinCodeAsync`로 채우는 기존 방식 유지.
- **`MatchmakerManager.DetermineIsHostAsync` / `GetStableHash` 비활성화**: 계획대로 즉시 삭제가 아니라 블록 주석(`/* */`)으로 비활성화. 상단에 폐기 사유와 참조 task 경로를 주석으로 명시.
- **호스트/클라이언트 판별**: 기존 `LobbyManager.IsHost`(`CurrentLobby.HostId == 내 PlayerId`) 재사용. 계획과 동일.

### 계획 시점 미확정 → 이번에 확정된 항목
- **[Research §8 / 위험요소 1 해소] CreateOrJoin SDK 시그니처 확정**: `com.unity.services.multiplayer@2.0.0`에서 `LobbyService.Instance.CreateOrJoinLobbyAsync(string lobbyId, string lobbyName, int maxPlayers, CreateLobbyOptions options = null)` 형태로 확정. **`matchId`를 `lobbyId`로 직접 사용 가능**함을 확인(별도 키 매핑 불필요). 다만 이는 공식 문서 기준으로 확정한 것으로, **에디터 컴파일로 최종 확인하는 것을 권장**(잔여 리스크 ①).
- **[위험요소 3 해소] 클라이언트 참가 경로 일원화**: 구 클라 참가 경로(`NetworkGameManager.JoinByMatchIdAsync` → `FindLobbyByMatchIdAsync` 폴링 → `JoinGameByIdAsync`)를 **비활성화(블록 주석)** 하고, 클라이언트 참가를 CreateOrJoin 한 번으로 일원화. CreateOrJoin으로 이미 Lobby에 참가되므로 별도 로비 검색 폴링이 불필요. 남은 대기는 "RelayJoinCode 채워짐 대기"뿐.
  - **`LobbyManager.FindLobbyByMatchIdAsync`는 미사용화되었으나 삭제하지 않음**(구 경로가 아직 주석으로만 비활성화 상태이므로 함께 보존).
- **[위험요소 2 반영] RelayJoinCode 대기 로직 신규 + `RefreshCurrentLobbyAsync` 추가**: 클라이언트가 호스트의 RelayJoinCode 기록을 기다리도록 폴링 대기 구현. `LobbyManager.RefreshCurrentLobbyAsync()`(내부 `LobbyService.Instance.GetLobbyAsync`로 `CurrentLobby` 최신화) 신규 추가. `CurrentLobby`는 참가 시점 스냅샷이라 나중에 채워진 RelayJoinCode가 반영되지 않으므로 재조회가 필요. 대기 상한은 **최대 15회(약 15초) 폴링**, 초과 시 에러 처리(잔여 리스크 ③).

### NetworkGameManager 구조 변경(계획의 "흐름 재구성" 구체화)
- **신규 `StartMatchmadeGameAsync(matchId)`**: 매칭 성사 후 진입점. CreateOrJoin 호출 → `IsHost`로 분기.
- **신규 `HostMatchmadeGameAsync(lobbyCode)`**: 호스트 경로. Relay 할당(`_relayManager.CreateRelayAsync`) → `UpdateRelayJoinCodeAsync`로 JoinCode를 Lobby에 기록 → Host 시작.
- **신규 `JoinMatchmadeGameAsync()`**: 클라이언트 경로. RelayJoinCode 채워짐 대기(위 15회 폴링) → Relay 참가 → Client 시작.
- **`StartMatchmakingAsync` 분기 교체**: 기존 `DetermineIsHostAsync` 호출 + `HostGameAsync`/`JoinByMatchIdAsync` 분기를 `StartMatchmadeGameAsync(matchId)` 단일 호출로 대체.

### 실제 변경 파일 (3개, 모두 Infrastructure/Network)
```
[수정]
- Assets/_Project/Scripts/Infrastructure/Network/LobbyManager.cs
    · [추가] CreateOrJoinLobbyByMatchIdAsync, RefreshCurrentLobbyAsync
    · FindLobbyByMatchIdAsync 는 미사용화(삭제 안 함)
- Assets/_Project/Scripts/Infrastructure/Network/MatchmakerManager.cs
    · [비활성화] DetermineIsHostAsync, GetStableHash (블록 주석)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs
    · [추가] StartMatchmadeGameAsync, HostMatchmadeGameAsync, JoinMatchmadeGameAsync
    · [수정] StartMatchmakingAsync 분기 교체
    · [비활성화] 구 클라 참가 경로(JoinByMatchIdAsync/JoinGameByIdAsync) 블록 주석
```
브랜치 `claude/matchmaker-404-error-pi9qdn`, 커밋 `a3dbc73`.

### 남은 잔여 리스크 (지속 테스트로 확인 필요)
1. **SDK 시그니처 최종 확인**: 공식 문서 기준으로 확정했으나, 에디터 컴파일로 최종 검증 권장.
2. **"정확히 한 명만 호스트" 및 간헐 재현**: CreateOrJoin 서버 원자성 전제는 맞으나, 실제 동시 요청 시 한쪽이 반드시 참가로 귀결되는지 + 간헐 404 재발 여부는 **지속 멀티 실기(Host+Client) 검증 필요**.
3. **클라 RelayJoinCode 대기 15초 타임아웃**: 호스트 Relay 할당이 지연되면 15초 내 미수신으로 실패할 수 있음.

> ⚠️ 지속 테스트가 확정 PASS로 마무리되면: ① 비활성화(주석)한 레거시 코드(`DetermineIsHostAsync`/`GetStableHash`, 구 클라 참가 경로)와 미사용 `FindLobbyByMatchIdAsync`의 최종 삭제 여부 결정, ② 본 task 상태를 "확정 완료"로 갱신하는 후속 문서 반영을 진행한다.
> **참고(환경):** 로컬 사용자 MEMORY(`C:/Users/rmsep/.claude/...`)는 원격(Linux) 세션에서 접근 불가하여 이번 갱신에서 미접근.
