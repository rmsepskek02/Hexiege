# Research: 랜덤 매칭 시스템

**날짜:** 2026-03-15

---

## 1. UGS Matchmaker 서비스 개요

### 핵심 개념

| 용어 | 설명 |
|------|------|
| **Queue** | 매칭을 처리하는 단위. 플레이어가 티켓을 제출하는 대상. 게임 모드별로 Queue를 분리 |
| **Ticket** | 플레이어가 매칭을 요청할 때 생성하는 객체. 상태 폴링으로 매칭 완료 여부 확인 |
| **Match** | Matchmaker가 조건을 만족하는 플레이어를 묶어 생성하는 결과물 |
| **Pool** | Queue 내부의 플레이어 그룹. 조건(레이팅 등)으로 분리 가능. 순수 랜덤은 Pool 1개면 됨 |
| **Rule** | 매칭 조건. 순수 랜덤은 규칙 없이 인원 수(2명)만 지정 |

### 티켓 상태 흐름

```
InProgress → Timeout (타임아웃 없음으로 설정) / Failed
           → Found (매칭 완료) → MatchProperties 반환
```

---

## 2. UGS 대시보드 설정 항목

### 2-1. Matchmaker 서비스 활성화
- UGS 대시보드 → 프로젝트 선택 → **Matchmaker** 탭
- "Enable Matchmaker" 활성화

### 2-2. Environment 확인
- Development / Production 환경 구분
- 개발 중: **Development** 환경 사용
- Unity 에디터에서 `UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName("development"))` 로 맞춰야 함

### 2-3. Queue 생성
경로: Matchmaker → **Queues** → Create Queue

| 설정 항목 | 값 | 설명 |
|-----------|-----|------|
| **Queue Name** | `hexiege-random` | 코드에서 참조할 Queue ID |
| **Maximum Playaers** | `2` | 매치당 최대 플레이어 수 |
| **Minimum Players** | `2` | 매치 성사 최소 인원 |
| **Timeout** | `0` (또는 최대값) | 매칭 타임아웃 없음 |

### 2-4. Pool 생성
Queue 생성 후 → **Pools** → Add Pool

| 설정 항목 | 값 |
|-----------|-----|
| **Pool Name** | `default-pool` |
| **Timeout** | `0` |

### 2-5. Rule 설정
순수 랜덤이므로 Rule 추가 없음. Pool에 인원 조건(2명)만 설정.

---

## 3. UGS Matchmaker SDK (패키지)

### 패키지 ID
```
com.unity.services.multiplayer
```
Package Manager → Add package by name으로 설치.
> ⚠️ `com.unity.services.matchmaker` 1.2.0은 2025년 2월 deprecated.
> Matchmaker SDK가 `com.unity.services.multiplayer`로 통합됨.
> 네임스페이스(`Unity.Services.Matchmaker`)와 API는 동일하게 유지됨.

### 주요 API

```csharp
// 티켓 생성
var ticketResponse = await MatchmakerService.Instance.CreateTicketAsync(
    new List<Player> { new Player(AuthenticationService.Instance.PlayerId) },
    new CreateTicketOptions { QueueName = "hexiege-random" }
);
string ticketId = ticketResponse.Id;

// 티켓 상태 폴링
var statusResponse = await MatchmakerService.Instance.GetTicketAsync(ticketId);
// statusResponse.Type: InProgress / Found / Failed / Timeout

// 매칭 완료 시 결과
if (statusResponse.Type == typeof(MultiplayAssignment)) { ... }
if (statusResponse.Type == typeof(MatchmakingResults)) { ... } // Client-only 매칭

// 티켓 취소
await MatchmakerService.Instance.DeleteTicketAsync(ticketId);
```

### 매칭 결과 구조 (MatchmakingResults)
```csharp
var result = statusResponse.Value as MatchmakingResults;
result.MatchId       // 매치 고유 ID (UUID)
result.Players       // 매치된 플레이어 목록 (List<Player>)
```

---

## 4. Host/Client 결정 방식

### 요구사항
- 알파벳 순 정렬 방식 대신 **무작위** 결정
- 두 플레이어가 서버 통신 없이 동일한 결론 도출 필요 (결정론적 랜덤)

### 채택 방식: MatchId 해시 기반 결정론적 랜덤
```csharp
// MatchId (UUID 문자열)를 해시값으로 변환
int hash = matchId.GetHashCode();

// 매칭된 플레이어 목록을 PlayerId 기준으로 정렬 (일관성 보장)
var sortedPlayers = result.Players.OrderBy(p => p.Id).ToList();

// 해시 기반으로 Host 인덱스 결정 (0 or 1 — UUID 기반이라 실질적으로 랜덤)
int hostIndex = Math.Abs(hash) % sortedPlayers.Count;

// 내가 Host인지 확인
bool isHost = sortedPlayers[hostIndex].Id == AuthenticationService.Instance.PlayerId;
```
- MatchId는 UUID(랜덤 생성)이므로 호스트 결정이 실질적으로 무작위
- 두 플레이어 모두 동일한 계산으로 일관된 Host/Client 판단 가능

---

## 5. Relay 연결 방식 (서버 없는 P2P)

Matchmaker는 플레이어를 매칭할 뿐, Relay 연결은 직접 처리해야 함.

### 채택 방식: Lobby 기반 랑데부
```
Host  → Relay 생성 → 공개 Lobby 생성 (data["matchId"] = matchId, maxPlayers=2)
Client → QueryLobbies 필터 (data["matchId"] = matchId) → Lobby 발견 → Join
```
- 기존 `NetworkGameManager.HostGameAsync()` / `JoinGameAsync()` 내부 로직 재활용
- 커스텀 게임과 동일한 Relay+NGO 흐름으로 연결

### Client Lobby 폴링 전략
- Host가 Lobby를 생성하는 데 1~2초 소요될 수 있음
- Client는 최대 10회 × 1초 간격으로 재시도 (총 10초 대기)

---

## 6. 대기 시간 표시

- `RandomMatchView._statusText` 이미 존재
- `BattleViewModel.MatchWaitSeconds` (ReactiveProperty<int>) 추가
- 1초마다 증가하는 타이머 → `"매칭 중... 00:13"` 포맷으로 표시
- 매칭 완료 또는 취소 시 타이머 정지

---

## 7. 영향 범위

| 구분 | 파일 | 작업 |
|------|------|------|
| 신규 | `Infrastructure/Network/MatchmakerManager.cs` | Matchmaker SDK 래퍼 |
| 수정 | `Infrastructure/Network/NetworkGameManager.cs` | StartMatchmakingAsync / CancelMatchmaking 추가 |
| 수정 | `Presentation/UI/ViewModels/BattleViewModel.cs` | CmdStartMatchmaking 로직 + 타이머 |
| 수정 | `Presentation/UI/Views/Lobby/Battle/RandomMatchView.cs` | 타이머 텍스트 바인딩 |
| 패키지 | `Packages/manifest.json` | com.unity.services.matchmaker 추가 |
| 외부 | UGS 대시보드 | Queue/Pool 설정 |
