# Research — 랜덤 매칭 후 종족 캐릭터 잘못 표시 버그

## 작업 개요

랜덤 매칭이 완료되어 씬 전환이 진행되는 중간에, 로비의 종족 선택 화면(캐러셀 UI)에서 내가 선택한 종족 캐릭터가 다른 종족 캐릭터로 잠깐 바뀌어 보이는 현상이다. 예를 들어 B 플레이어가 "불꽃정령"을 선택했는데 매칭 완료 후 중앙 선택 위치에 A 플레이어의 "권총병(인간족)"이 표시된다. 게임에 진입하면 종족 데이터 자체는 올바르게 적용된다.

즉 **데이터 문제가 아닌 UI 표시(렌더링) 타이밍 문제**다.

---

## 관련 파일 목록

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs` | 캐러셀 3D 캐릭터 배치 로직 |
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/RaceSelectionViewModel.cs` | 선택된 종족 상태 관리 |
| `Assets/_Project/Scripts/Infrastructure/LocalPlayerRace.cs` | 로컬 플레이어 종족 전역 정적 홀더 |
| `Assets/_Project/Scripts/Infrastructure/GameRaceContext.cs` | 인게임 양 팀 종족 정보 전역 홀더 |
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleRootView.cs` | 전투 탭 루트 — RaceSelectionViewModel 생성 담당 |
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleMainView.cs` | 종족 선택 뷰 포함, CurrentScreen=Main일 때만 활성화 |
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` | 매칭 상태·화면 상태(CurrentScreen) 관리 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs` | 랜덤 매칭 흐름 총괄 (DontDestroyOnLoad) |
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs` | 씬 초기화 — BattleViewModel/BattleRootView 바인딩 1회 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs` | 게임 씬에서 종족 정보 동기화 (RequestReadyServerRpc) |

---

## 아키텍처 파악

### 종족 선택 → 저장 흐름

```
사용자가 화살표 클릭
  → RaceSelectionView (버튼 이벤트)
  → RaceSelectionViewModel.CmdPrev / CmdNext
  → SelectedRace.Value 변경 (ReactiveProperty)
  → ApplyCarouselPositions() 호출 (캐릭터 위치 변경)
  → LocalPlayerRace.Set(race) 즉시 저장
```

- `RaceSelectionViewModel.SelectedRace`는 생성 시 `LocalPlayerRace.Current`로 초기화
- Subscribe 시 즉시 현재 값을 발행 → 초기 캐릭터 위치도 `LocalPlayerRace.Current` 기준

### 매칭 → 씬 전환 흐름

```
[BattleViewModel] CmdStartMatchmaking 처리
  1. CurrentScreen = RandomMatch  → BattleMainView 비활성화
  2. await StartMatchmakingAsync()
       → 매칭 완료: onMatchFound() → LoadingScreen 표시
       → DetermineIsHostAsync() → Host/Client 결정
       → Host: HostGameAsync() → Relay+Lobby 생성 → StartNetworkHost()
                                → OnHostStarted 이벤트 → ConnectedPlayers = 1
         Client: JoinByMatchIdAsync() → JoinGameByIdAsync() → StartNetworkClient()
                                      → OnClientConnected 이벤트 → ConnectedPlayers = 1
  3. Host 측: Client 접속 감지 → HandleClientConnected → OnClientConnected 이벤트
             → ConnectedPlayers = 2 → LoadGameScene()
  4. NGO SceneManager → Game 씬 로드 → Lobby 씬 언로드
             → LobbyRootView.OnDestroy() → BattleRootView.Unbind() → _raceVm.Dispose()
```

### BattleMainView SetActive 조건

```csharp
// BattleMainView.Bind()
vm.CurrentScreen
    .Select(s => s == BattleViewModel.BattleScreen.Main)
    .Subscribe(visible => gameObject.SetActive(visible));
```

- `CurrentScreen = RandomMatch` → BattleMainView `SetActive(false)`
- `CurrentScreen = Main` → BattleMainView `SetActive(true)` → 캐릭터 캐러셀 다시 보임

---

## 버그 의심 지점

### [의심 1] CurrentScreen이 예기치 않게 Main으로 복귀하는 경우

`BattleViewModel.CmdStartMatchmaking` 핸들러:

```csharp
catch (OperationCanceledException)
{
    IsMatchmaking.Value = false;
    CurrentScreen.Value = BattleScreen.Main;   // ← BattleMainView 활성화!
    LoadingScreen.Instance?.Hide();
}
```

`OperationCanceledException`은 `_matchmakingCts.Cancel()` 호출 시 `Task.Delay(..., ct)` 내부에서 발생한다.
`CancelMatchmakingAsync()`에서 `_matchmakingCts.Cancel()`을 호출한다.

**문제**: 사용자가 취소 버튼을 누르지 않았는데도 이 경로로 진입하면 `CurrentScreen = Main`이 되어 `BattleMainView`가 활성화된다. 이 시점에 `LocalPlayerRace.Current`가 Human이라면 권총병이 중앙에 표시된다.

→ 그러나 현재 코드상 취소 버튼 클릭 외에 `_matchmakingCts.Cancel()`이 호출되는 경로는 보이지 않아 확정 불가.

---

### [의심 2] BattleRootView 재바인딩 시 LocalPlayerRace 초기값 문제

`BattleRootView.Bind(vm)`:
```csharp
_raceVm = new RaceSelectionViewModel();  // LocalPlayerRace.Current로 초기화
```

`RaceSelectionViewModel` 생성자:
```csharp
public ReactiveProperty<RaceId> SelectedRace { get; } = new(LocalPlayerRace.Current);
```

Subscribe 등록 시 즉시 현재 값 발행 → `ApplyCarouselPositions(LocalPlayerRace.Current, animate:false)`.

**문제**: 만약 `BattleRootView.Bind()`가 재호출되는 시점에 `LocalPlayerRace.Current`가 Human이라면, 캐러셀이 Human 기준으로 초기 배치된다.

현재 코드에서 `BattleRootView.Bind()`는 `LobbyRootView.Start()`에서 1회만 호출된다. 하지만 **로비 씬이 재로드될 때마다** `Start()`가 다시 실행된다.

`LocalPlayerRace.Reset()`은 코드 전체에서 **어디에도 호출되지 않는다** → 로비 복귀 후에도 이전 종족 값이 유지됨 → 이 경로에서 Human이 될 가능성 낮음.

→ 정상 플로우에서는 발생하기 어려움. 단, 처음 로비 진입 시(`LocalPlayerRace.Current == Human` 기본값)에 사용자가 종족을 선택하기 전에 매칭을 시작한다면 Human이 표시될 수 있음.

---

### [의심 3] _characterRoots가 World Space 오브젝트이고 BattleMainView 비활성화와 무관

`RaceSelectionView`의 `_characterRoots`는 World Space에 별도로 배치된 3D 오브젝트이다. `BattleMainView.SetActive(false)`가 되어도 `_characterRoots`는 여전히 씬에 남아 있다.

단, `RawImage`(`_rawImage`)는 `BattleMainView`의 자식이면 함께 비활성화되어 보이지 않아야 한다.

→ Inspector 연결 구조에 따라 실제 표시 여부가 달라진다. `_rawImage`가 BattleMainView 외부에 있다면 항상 보일 수 있다.

---

### [의심 4] async void 패턴과 Subject.Subscribe 타이밍 경쟁

`CmdStartMatchmaking.Subscribe(async _ => ...)` 패턴은 UniRx에서 `async void`처럼 동작한다. `await StartMatchmakingAsync()` 완료 후 `CmdStartMatchmaking` try 블록이 종료되는 시점과 `OnClientConnected` 이벤트가 발생하는 시점 사이에 미묘한 타이밍 차이가 있을 수 있다.

특히 **Client 측에서**:
- `JoinGameByIdAsync()` → `OnClientConnected?.Invoke()` → `ConnectedPlayers = 1`
- 동시에 Host NGO SceneManager가 씬 전환 명령 전송
- Client가 씬 전환 명령 수신 → `LobbyRootView.OnDestroy()` 호출

이 두 가지가 거의 동시에 일어날 경우 `BattleRootView.Unbind()` 타이밍과 씬 전환 시작 타이밍이 엇갈릴 수 있다.

---

### [의심 5] ConnectedPlayers 값 누적 가능성

`BattleViewModel.OnClientConnected()`:
```csharp
private void OnClientConnected()
{
    ConnectedPlayers.Value++;
    if (ConnectedPlayers.Value >= 2)
    {
        LoadingScreen.Instance?.Show("게임에 접속하는 중...");
        _networkManager.LoadGameScene();
    }
}
```

`BattleViewModel`은 씬 초기화 시 새로 생성되므로 `ConnectedPlayers = 0`이 보장된다. 단, `NetworkGameManager.OnClientConnected`에 등록된 이벤트 핸들러가 중복 등록된다면 `ConnectedPlayers`가 2 이상이 되어 `LoadGameScene()`이 의도치 않게 조기 호출될 수 있다.

`NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected`는 `HostGameAsync()` 내에서 등록되고, `BackToLobby()` 또는 `DisconnectAsync()`에서 해제된다. 정상적인 플로우에서는 중복 등록이 없다.

---

## 게임 씬에서의 종족 정보 흐름 (참고)

버그가 게임 씬이 아닌 로비 씬의 UI 표시 문제임을 확인.

`NetworkGameFlow.WaitForTeamAndSendReady()`:
```csharp
int myRace = (int)LocalPlayerRace.Current;  // 로비에서 선택한 값
RequestReadyServerRpc(myRace);
```

서버에서 `StartGameClientRpc(blueRace, redRace)` 전송 → `GameRaceContext.Set()` → 인게임 종족 초기화.

인게임에서는 `LocalPlayerRace.Current`가 아닌 `GameRaceContext`를 사용하므로 게임 데이터는 정상.

---

## 추가 확인이 필요한 항목

1. **`_rawImage`가 Inspector에서 어느 오브젝트의 자식인지** — `BattleMainView` 외부에 있다면 항상 렌더링됨
2. **매칭 완료~씬 전환 사이에 `CurrentScreen`이 변경되는 경우** — 특히 예외 경로 재확인
3. **`LoadingScreen`이 표시되기 전 프레임에 캐러셀이 갱신되는지** — 로딩 스크린 애니메이션 시간이 있다면 그 사이에 노출 가능
4. **BattleMainView 비활성화 후에도 RaceSelectionView 구독이 살아있어 캐릭터 위치가 변경될 수 있는지** — 구독은 `_disposables`에 묶여 있어 Unbind 전까지 살아있음

---

## 현재 파악 요약

- **로비 종족 선택 UI(캐러셀)에서 매칭 완료 직후 잘못된 종족 캐릭터가 중앙에 배치되어 표시되는 현상**
- **실제 게임 데이터(`LocalPlayerRace.Current`, `GameRaceContext`)는 정상**
- `RaceSelectionView.ApplyCarouselPositions()`는 `SelectedRace.Value`가 변경될 때만 호출됨
- `SelectedRace.Value`가 의도치 않게 Human으로 변경되거나, `RaceSelectionView.Bind()`가 재호출되어 Human 기준으로 재배치되는 경우에 버그가 발생
- 가장 유력한 경로: **매칭 완료 후 어떤 경우에 `CurrentScreen = Main`이 되어 `BattleMainView`가 활성화되고, 그 순간 `LocalPlayerRace.Current` 기반의 캐릭터 위치 재배치가 발생**하는 타이밍 버그로 추정
- **간혹 발생한다는 점은 Race Condition(타이밍에 따라 결과가 달라지는 현상)임을 시사**
