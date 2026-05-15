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

## 2차 조사에서 추가로 확인된 사항 (2026-05-15)

### 코드 레벨에서 배제된 경로

| 항목 | 확인 결과 |
|------|-----------|
| `LocalPlayerRace.Reset()` 호출 여부 | 코드 전체 grep 결과 호출 없음 — Human으로 되돌아가는 경로 아님 |
| `DOTween.Kill` 전역 호출 여부 | `RaceSelectionView.cs` 두 곳에만 존재, 전역 `DOTween.KillAll()` 없음 |
| `BattleMainView.OnEnable`/`OnDisable` | 존재하지 않음 — `SetActive` 시 자동 Rebind 없음 |
| `RandomMatchView`의 캐릭터 관련 코드 | 없음 — UI 텍스트·버튼만 처리 |
| `LobbyViewModel.CurrentTab` 기본값 | `LobbyTab.Battle` — 탭 전환으로 인한 BattlePanel 비활성화 없음 |
| `LoadingScreen.Show()` 의 씬 영향 | 없음 — CanvasGroup.DOFade만 실행, Sort Order 999 오버레이 |
| `_randomMatchButton` 비활성화 타이밍 | `BattleMainView.SetActive(false)` 시 함께 비활성화 → 버튼 재클릭 불가 |

### 코드 추적으로 확인된 정상 흐름

`ApplyCarouselPositions(Spirit, animate:true)` 호출 시 각 캐릭터의 예상 이동:

| 캐릭터 | offset | 시작 위치 | 목표 위치 | DOTween |
|--------|--------|-----------|-----------|---------|
| Human (i=0) | 2 → 왼쪽 | `_centerPos` | `_leftPos` | 1초 이동 |
| Spirit (i=1) | 0 → 중앙 | `_rightPos` | `_centerPos` | 1초 이동 |
| Transcendence (i=2) | 1 → 오른쪽 | `_leftPos` | `_rightPos` | 1초 이동 |

1초 완료 후 Human은 `_leftPos`에, Spirit은 `_centerPos`에 있어야 한다.

### 현재 로그에서 2차 ApplyCarouselPositions(Human) 호출이 보이지 않는 이유

세 가지 가능성 중 코드상으로 확인 불가한 사항:

1. **Inspector `_characterRoots[0]`이 Human이 아닌 다른 캐릭터인 경우** — 배열 인덱스가 잘못 연결되어 있으면 offset 계산이 다른 캐릭터에 적용됨
2. **Inspector `_leftPos` / `_centerPos` 값이 뒤바뀌어 설정된 경우** — Human DOTween이 `_leftPos`를 향하지만 실제로 그 값이 중앙 좌표라면 Human은 중앙에 남음
3. **`_moveDuration`이 매우 길게 설정된 경우** — 로딩 화면이 나타날 시점에도 DOTween이 실행 중이어서 Human이 아직 중앙 근처에 있음

→ **위 세 항목은 코드 읽기만으로 확인 불가. 런타임 로그에 실제 Inspector 값을 기록해야 함.**

## 추가 확인이 필요한 항목

1. **`ApplyCarouselPositions` 호출 시 실제 Inspector 위치 값** — `_centerPos`, `_leftPos`, `_rightPos`, `_moveDuration` 실제 값 로그 기록
2. **각 캐릭터의 실제 현재 위치(transform.position)** — 이동 직전과 이동 완료 후 비교
3. **`KillAllCharacterTweens()` 호출 타이밍** — Unbind 이전에 호출되는지 여부 확인
4. **`_rawImage`가 Inspector에서 어느 오브젝트의 자식인지** — `BattleMainView` 외부에 있다면 항상 렌더링됨

---

## 현재 파악 요약

- **로비 종족 선택 UI(캐러셀)에서 매칭 완료 직후 잘못된 종족 캐릭터가 중앙에 배치되어 표시되는 현상**
- **실제 게임 데이터(`LocalPlayerRace.Current`, `GameRaceContext`)는 정상**
- `RaceSelectionView.ApplyCarouselPositions()`는 `SelectedRace.Value`가 변경될 때만 호출됨
- `SelectedRace.Value`가 의도치 않게 Human으로 변경되거나, `RaceSelectionView.Bind()`가 재호출되어 Human 기준으로 재배치되는 경우에 버그가 발생
- 가장 유력한 경로: **매칭 완료 후 어떤 경우에 `CurrentScreen = Main`이 되어 `BattleMainView`가 활성화되고, 그 순간 `LocalPlayerRace.Current` 기반의 캐릭터 위치 재배치가 발생**하는 타이밍 버그로 추정
- **간혹 발생한다는 점은 Race Condition(타이밍에 따라 결과가 달라지는 현상)임을 시사**

---

## 최종 원인 확정 (2026-05-15)

### 런타임 로그 기반 분석 결과

2차 강화 로그(Inspector 실제값, 캐릭터 transform 위치, KillAllCharacterTweens 호출 추적)를 Red팀 클라이언트에서 수집한 결과, 코드 실행 경로 자체는 완전히 정상이었다.

- `ApplyCarouselPositions` 호출 횟수, 인수, 목표 위치 모두 정상
- Inspector 위치값(`centerPos`/`leftPos`/`rightPos`) 정상, 배열 순서 정상
- 버그 발생 구간(클라이언트 연결 후 ~ 씬 전환 전)에 캐러셀 관련 코드 일체 실행 없음

### 실제 원인: NetworkTransform 위치 동기화

**Lobby 씬의 세 캐릭터 프리뷰 오브젝트가 실제 게임 유닛 프리팹 인스턴스였다.**

| 씬 오브젝트 | 원본 프리팹 |
|------------|------------|
| `CharPreview_Human` | `Unit_Pistoleer_Blue.prefab` |
| `CharPreview_Spirit` | `Unit_EmberSpirit_Blue.prefab` |
| `CharPreview_Transcendence` | `Unit_FoxMagician_Blue.prefab` |

게임 유닛 프리팹에는 에디터 스크립트로 자동 추가된 `NetworkObject` + `NetworkTransform`이 포함되어 있다.

**버그 발생 흐름:**
1. Red 클라이언트가 릴레이에 연결되는 순간(`OnClientConnected`) NGO가 씬의 `NetworkObject`를 인식
2. `NetworkTransform`이 **호스트(Blue)의 캐릭터 위치를 클라이언트에게 동기화**
3. 호스트는 Human을 선택한 상태(Human이 `centerPos`) → 이 위치가 클라이언트로 전송
4. 클라이언트는 Spirit을 선택해 DOTween으로 Spirit→중앙, Human→왼쪽으로 이동시켰지만, NetworkTransform이 Human→중앙 위치로 덮어씀
5. 결과: 애니메이션은 로컬 선택 기준(Spirit=Walk, Human=Idle)이고, 위치는 호스트 기준(Human=중앙) → 불일치

### 수정 방법

Lobby 씬에서 세 캐릭터 프리뷰 오브젝트를 **프리팹 언팩** 후, 로비 프리뷰에 불필요한 네트워크 컴포넌트와 게임 로직 컴포넌트를 제거했다.

제거한 컴포넌트: `UnitView`, `AnimationEventRelay`, `NetworkUnit`, `NetworkTransform`, `NetworkObject`
유지한 컴포넌트: `Transform`, `Animator`, `SkinnedMeshRenderer` 및 메시 자식 오브젝트
