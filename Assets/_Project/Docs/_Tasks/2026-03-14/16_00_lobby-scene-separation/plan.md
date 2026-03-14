# Plan: Lobby/Game 씬 분리 + Lobby UI (MVVM + UniRx)

**날짜:** 2026-03-14
**작업자:** Claude

---

## 목표

1. `Game.unity` 단일 씬을 `Lobby.unity` / `Game.unity`로 분리
2. Lobby 씬에 MVVM + UniRx 기반 탭 UI 구조 구축
3. 전투 탭: 싱글플레이 / 커스텀 게임 / 랜덤 매칭 3가지 진입 경로

---

## UI 패턴: MVVM + UniRx

### 구조

```
Model (기존 유지)
  └── NetworkGameManager, UseCases, Domain

ViewModel (순수 C# 클래스 — UnityEngine 의존 없음)
  ├── ReactiveProperty<T>  : 상태 (View가 구독)
  └── Subject<T>           : 커맨드 (View가 OnNext 호출)

View (MonoBehaviour)
  ├── Bind(ViewModel)      : ReactiveProperty 구독 설정
  ├── Unbind()             : 구독 해제
  └── UI 이벤트 → ViewModel Subject로 전달 (로직 없음)
```

### 핵심 바인딩 패턴

```csharp
// ViewModel — 순수 C#
public class BattleViewModel : IDisposable
{
    public ReactiveProperty<string> LobbyCode = new("");
    public ReactiveProperty<bool> IsConnecting = new(false);
    public Subject<Unit> OnStartHosting = new();
    public Subject<string> OnJoinGame = new();
}

// View — MonoBehaviour, 로직 없이 바인딩만
public class CustomHostView : MonoBehaviour
{
    [SerializeField] private TMP_Text _codeText;
    [SerializeField] private Button _cancelButton;
    private CompositeDisposable _disposables = new();

    public void Bind(BattleViewModel vm)
    {
        vm.LobbyCode
            .Subscribe(code => _codeText.text = code)
            .AddTo(_disposables);

        _cancelButton.onClick.AddListener(
            () => vm.OnCancelHosting.OnNext(Unit.Default));
    }

    public void Unbind() => _disposables.Clear();
}
```

### 화면 전환: 상태 기반 (State-Driven Navigation)

ViewModel의 `CurrentScreen` ReactiveProperty로 화면 전환.
별도 Screen Manager 불필요 — 각 View가 스스로 활성화/비활성화.

```csharp
// BattleViewModel
public enum BattleScreen { Main, CustomGame, CustomHost, CustomJoin, RandomMatch }
public ReactiveProperty<BattleScreen> CurrentScreen = new(BattleScreen.Main);

// CustomHostView.Bind()
vm.CurrentScreen
    .Select(s => s == BattleScreen.CustomHost)
    .DistinctUntilChanged()
    .Subscribe(visible => gameObject.SetActive(visible))
    .AddTo(_disposables);
```

---

## 전체 파일 구조

```
Assets/_Project/Scripts/Presentation/UI/
│
├── Core/
│   └── IView.cs                        ← View 공통 인터페이스
│
├── ViewModels/
│   ├── LobbyViewModel.cs               ← 탭 전환 상태
│   └── BattleViewModel.cs              ← 전투 탭 전체 상태 + 커맨드
│
└── Views/
    └── Lobby/
        ├── LobbyRootView.cs            ← ViewModel 생성/주입, 루트 진입점
        ├── TabBarView.cs               ← 탭 버튼 바인딩
        ├── Battle/
        │   ├── BattleRootView.cs       ← BattleViewModel 생성, 서브뷰 Bind
        │   ├── BattleMainView.cs       ← 싱글/커스텀/랜덤 선택
        │   ├── CustomGameView.cs       ← 방 만들기 / 코드 참가 선택
        │   ├── CustomHostView.cs       ← 코드 표시 + 대기
        │   ├── CustomJoinView.cs       ← InputField + 참가 버튼
        │   └── RandomMatchView.cs      ← 매칭 대기 + 취소
        ├── Shop/
        │   └── ShopView.cs             ← 플레이스홀더
        ├── Profile/
        │   └── ProfileView.cs          ← 플레이스홀더
        └── Ranking/
            └── RankingView.cs          ← 플레이스홀더
```

---

## ViewModel 상세 설계

### IView.cs

```csharp
namespace Hexiege.Presentation
{
    public interface IView<TViewModel>
    {
        void Bind(TViewModel viewModel);
        void Unbind();
    }
}
```

### LobbyViewModel.cs

```csharp
using UniRx;

namespace Hexiege.Presentation
{
    public class LobbyViewModel : System.IDisposable
    {
        public enum LobbyTab { Battle, Shop, Profile, Ranking }

        // 현재 활성 탭
        public ReactiveProperty<LobbyTab> CurrentTab = new(LobbyTab.Battle);

        // 탭 전환 커맨드
        public Subject<LobbyTab> SelectTab = new();

        private CompositeDisposable _disposables = new();

        public LobbyViewModel()
        {
            SelectTab
                .Subscribe(tab => CurrentTab.Value = tab)
                .AddTo(_disposables);
        }

        public void Dispose() => _disposables.Dispose();
    }
}
```

### BattleViewModel.cs

```csharp
using UniRx;
using System;

namespace Hexiege.Presentation
{
    public class BattleViewModel : IDisposable
    {
        public enum BattleScreen
        {
            Main, CustomGame, CustomHost, CustomJoin, RandomMatch
        }

        // ── 화면 상태 ──────────────────────────────────────────────
        public ReactiveProperty<BattleScreen> CurrentScreen = new(BattleScreen.Main);

        // ── 네트워크 상태 ─────────────────────────────────────────
        public ReactiveProperty<string>  LobbyCode           = new("");
        public ReactiveProperty<bool>    IsConnecting        = new(false);
        public ReactiveProperty<int>     ConnectedPlayers    = new(0);
        public ReactiveProperty<string>  ErrorMessage        = new("");
        public ReactiveProperty<bool>    IsMatchmaking       = new(false);

        // ── 커맨드 (View → ViewModel) ─────────────────────────────
        public Subject<Unit>   CmdStartSingleplay  = new();
        public Subject<Unit>   CmdStartHosting     = new();
        public Subject<string> CmdJoinGame         = new();
        public Subject<Unit>   CmdCancelHosting    = new();
        public Subject<Unit>   CmdStartMatchmaking = new();
        public Subject<Unit>   CmdCancelMatchmaking= new();
        public Subject<Unit>   CmdBack             = new();

        private readonly NetworkGameManager _networkManager;
        private CompositeDisposable _disposables = new();

        public BattleViewModel(NetworkGameManager networkManager)
        {
            _networkManager = networkManager;

            // 서비스 이벤트 → ReactiveProperty 업데이트
            networkManager.OnHostStarted += OnHostStarted;
            networkManager.OnClientConnected += OnClientConnected;
            networkManager.OnError += OnNetworkError;

            // 커맨드 처리
            CmdStartSingleplay
                .Subscribe(_ => LoadSingleplayScene())
                .AddTo(_disposables);

            CmdStartHosting
                .Subscribe(async _ => await StartHosting())
                .AddTo(_disposables);

            CmdJoinGame
                .Subscribe(async code => await JoinGame(code))
                .AddTo(_disposables);

            CmdCancelHosting
                .Subscribe(_ => CancelHosting())
                .AddTo(_disposables);

            CmdBack
                .Subscribe(_ => NavigateBack())
                .AddTo(_disposables);
        }

        // ── 내부 로직 ─────────────────────────────────────────────

        private void LoadSingleplayScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        }

        private async System.Threading.Tasks.Task StartHosting()
        {
            CurrentScreen.Value = BattleScreen.CustomHost;
            IsConnecting.Value = true;
            ErrorMessage.Value = "";
            await _networkManager.HostGameAsync();
        }

        private async System.Threading.Tasks.Task JoinGame(string code)
        {
            IsConnecting.Value = true;
            ErrorMessage.Value = "";
            await _networkManager.JoinGameAsync(code);
        }

        private void CancelHosting()
        {
            IsConnecting.Value = false;
            CurrentScreen.Value = BattleScreen.Main;
            _networkManager.DisconnectAsync();
        }

        private void NavigateBack()
        {
            CurrentScreen.Value = CurrentScreen.Value switch
            {
                BattleScreen.CustomHost  => BattleScreen.CustomGame,
                BattleScreen.CustomJoin  => BattleScreen.CustomGame,
                BattleScreen.CustomGame  => BattleScreen.Main,
                BattleScreen.RandomMatch => BattleScreen.Main,
                _                        => BattleScreen.Main
            };
        }

        private void OnHostStarted(string code)
        {
            LobbyCode.Value = code;
            IsConnecting.Value = false;
            ConnectedPlayers.Value = 1;
        }

        private void OnClientConnected()
        {
            ConnectedPlayers.Value++;
            if (ConnectedPlayers.Value >= 2)
                _networkManager.LoadGameScene();
        }

        private void OnNetworkError(string msg)
        {
            ErrorMessage.Value = msg;
            IsConnecting.Value = false;
        }

        public void Dispose()
        {
            _networkManager.OnHostStarted -= OnHostStarted;
            _networkManager.OnClientConnected -= OnClientConnected;
            _networkManager.OnError -= OnNetworkError;
            _disposables.Dispose();
        }
    }
}
```

---

## View 상세 설계

### LobbyRootView.cs

```csharp
// ViewModel 생성 및 전체 하위 View에 주입하는 루트
// Start() → new LobbyViewModel() → TabBarView.Bind(vm) → BattleRootView.Bind(vm)
// OnDestroy() → vm.Dispose()
```

### TabBarView.cs

```csharp
// 탭 버튼 4개 → vm.SelectTab.OnNext(LobbyTab.xxx)
// vm.CurrentTab.Subscribe → 선택된 탭 버튼 강조, ContentPanel 활성화
```

### BattleRootView.cs

```csharp
// new BattleViewModel(networkManager) 생성
// 모든 서브뷰(BattleMainView, CustomHostView 등) Bind(battleVm) 호출
// vm.CurrentScreen 변경 → 서브뷰들이 스스로 SetActive 처리
```

### BattleMainView.cs

```csharp
// [싱글플레이]   → vm.CmdStartSingleplay.OnNext(Unit.Default)
// [커스텀 게임] → vm.CurrentScreen.Value = BattleScreen.CustomGame
// [랜덤 매칭]   → vm.CmdStartMatchmaking.OnNext(Unit.Default)
```

### CustomHostView.cs

```csharp
// vm.LobbyCode.Subscribe → 코드 텍스트 업데이트
// vm.ConnectedPlayers.Subscribe → "n/2명 연결됨" 표시
// vm.ErrorMessage.Subscribe → 에러 텍스트 표시
// [취소] → vm.CmdCancelHosting.OnNext(Unit.Default)
```

### CustomJoinView.cs

```csharp
// TMP_InputField → 코드 입력
// [참가] → vm.CmdJoinGame.OnNext(inputField.text)
// vm.IsConnecting.Subscribe → 버튼 비활성화 / 로딩 표시
// vm.ErrorMessage.Subscribe → 에러 메시지 표시
// [뒤로] → vm.CmdBack.OnNext(Unit.Default)
```

### RandomMatchView.cs

```csharp
// Show() → vm.CmdStartMatchmaking.OnNext(Unit.Default)
// vm.IsMatchmaking.Subscribe → 스피너 표시
// [취소] → vm.CmdCancelMatchmaking.OnNext(Unit.Default)
// (현재 플레이스홀더, 실제 Matchmaker 연동은 추후)
```

---

## 씬 구성

### Lobby.unity 오브젝트 계층

```
[Managers]
  ├─ NetworkManager          (NGO, Game 씬에서 이동)
  └─ NetworkGameManager      (DontDestroyOnLoad)

[UI] Canvas
  └─ LobbyRoot               ← LobbyRootView 부착
       ├─ TopBar              (추후: 프로필 요약, 재화)
       ├─ TabBar              ← TabBarView 부착
       │    ├─ BattleTabBtn
       │    ├─ ShopTabBtn
       │    ├─ ProfileTabBtn
       │    └─ RankingTabBtn
       └─ ContentArea
            ├─ BattlePanel   ← BattleRootView 부착
            │    ├─ BattleMainPanel      ← BattleMainView
            │    ├─ CustomGamePanel      ← CustomGameView
            │    ├─ CustomHostPanel      ← CustomHostView
            │    ├─ CustomJoinPanel      ← CustomJoinView
            │    └─ RandomMatchPanel     ← RandomMatchView
            ├─ ShopPanel      ← ShopView (플레이스홀더)
            ├─ ProfilePanel   ← ProfileView (플레이스홀더)
            └─ RankingPanel   ← RankingView (플레이스홀더)
```

### Game.unity 변경 사항

- `NetworkManager` 제거 (Lobby 씬으로 이동)
- `LobbyUI` 오브젝트 및 LobbyPanel 제거
- `GameEndUI`: "로비로 돌아가기" 버튼 추가
- 나머지 모두 현행 유지

---

## NetworkGameManager 수정

```csharp
// 추가할 내용
public event Action<string> OnHostStarted;   // 기존 유지
public event Action OnClientConnected;        // 기존 유지
public event Action<string> OnError;          // 기존 유지

// 신규 추가
public void LoadGameScene()
{
    if (!NetworkManager.Singleton.IsServer) return;
    NetworkManager.Singleton.SceneManager
        .LoadScene("Game", LoadSceneMode.Single);
}
```

---

## GameEndUI + NetworkGameEndController 수정

```csharp
// GameEndUI.cs — "로비로" 버튼 추가
private void OnBackToLobbyClicked()
{
    Time.timeScale = 1f;
    if (NetworkContext.IsNetworkActive)
        _networkGameEndController.RequestBackToLobby();
    else
        SceneManager.LoadScene("Lobby");
}

// NetworkGameEndController.cs — ClientRpc 추가
public void RequestBackToLobby()
{
    if (!NetworkManager.Singleton.IsServer) return;
    BackToLobbyClientRpc();
}

[ClientRpc]
private void BackToLobbyClientRpc()
{
    if (NetworkManager.Singleton.IsServer)
        NetworkManager.Singleton.SceneManager
            .LoadScene("Lobby", LoadSceneMode.Single);
}
```

---

## Build Settings

```
Index 0: Assets/_Project/Scenes/Lobby.unity  ← 앱 시작 씬
Index 1: Assets/_Project/Scenes/Game.unity
```

---

## 파일 변경 요약

### 신규 생성
| 파일 | 위치 |
|------|------|
| `IView.cs` | `UI/Core/` |
| `LobbyViewModel.cs` | `UI/ViewModels/` |
| `BattleViewModel.cs` | `UI/ViewModels/` |
| `LobbyRootView.cs` | `UI/Views/Lobby/` |
| `TabBarView.cs` | `UI/Views/Lobby/` |
| `BattleRootView.cs` | `UI/Views/Lobby/Battle/` |
| `BattleMainView.cs` | `UI/Views/Lobby/Battle/` |
| `CustomGameView.cs` | `UI/Views/Lobby/Battle/` |
| `CustomHostView.cs` | `UI/Views/Lobby/Battle/` |
| `CustomJoinView.cs` | `UI/Views/Lobby/Battle/` |
| `RandomMatchView.cs` | `UI/Views/Lobby/Battle/` |
| `ShopView.cs` | `UI/Views/Lobby/Shop/` |
| `ProfileView.cs` | `UI/Views/Lobby/Profile/` |
| `RankingView.cs` | `UI/Views/Lobby/Ranking/` |
| `Lobby.unity` | `Scenes/` |

### 수정
| 파일 | 변경 내용 |
|------|---------|
| `NetworkGameManager.cs` | `LoadGameScene()` 추가 |
| `GameEndUI.cs` | "로비로" 버튼 + BackToLobby 로직 |
| `NetworkGameEndController.cs` | `BackToLobbyClientRpc` 추가 |
| `EditorBuildSettings.asset` | Lobby(0) + Game(1) 등록 |

### 삭제
| 항목 | 내용 |
|------|------|
| `LobbyUI.cs` | BattleViewModel + 서브뷰들로 대체 |
| Game 씬 LobbyPanel 오브젝트 | 제거 |
| Game 씬 NetworkManager | Lobby 씬으로 이동 |

### 변경 불필요
- `GameBootstrapper.cs`, `NetworkGameFlow.cs`
- 모든 NetworkController × 8
- `GameHudUI.cs`, `BuildingPlacementUI.cs`, `ProductionPanelUI.cs`

---

## 씬 전환 최종 흐름

```
앱 시작 → Lobby.unity
  LobbyRootView.Start()
    → new LobbyViewModel()
    → new BattleViewModel(networkManager)
    → 전투 탭 기본, BattleMainView 표시

[싱글플레이]
  CmdStartSingleplay → SceneManager.LoadScene("Game")
  GameBootstrapper.Start() → !IsNetworkActive → LoadMap() 즉시

[커스텀 게임 - 호스트]
  CmdStartHosting → NetworkGameManager.HostGameAsync()
  OnHostStarted → LobbyCode 업데이트
  OnClientConnected (connectedPlayers >= 2) → LoadGameScene()
  NGO SceneManager → 양쪽 Game.unity 로드

[커스텀 게임 - 참가]
  CmdJoinGame(code) → NetworkGameManager.JoinGameAsync(code)
  Host 측 LoadGameScene() NGO 자동 동기화

[게임 종료 → 로비]
  GameEndUI [로비로] → BackToLobbyClientRpc → NGO LoadScene("Lobby")
  → LobbyRootView 재초기화, 전투 탭 표시
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| NetworkGameManager 이벤트 미구독 해제 | BattleViewModel.Dispose()에서 반드시 -= 해제 |
| NGO SceneManager 서버 전용 | `IsServer` 체크 강제 |
| `Time.timeScale = 0` 상태 씬 전환 | LoadScene 전 항상 `Time.timeScale = 1f` |
| BattleViewModel async 메서드 예외 | try-catch → ErrorMessage.Value 업데이트 |
| ViewModel Dispose 누락 | LobbyRootView.OnDestroy()에서 체이닝 Dispose |

---

## Inspector 작업 가이드

> 코드 구현 완료 후, Unity 에디터에서 수행해야 하는 작업 목록.
> 각 단계는 순서대로 진행.

---

### STEP 1 — Lobby.unity 씬 생성 및 계층 구조 구축

Unity 메뉴 `File → New Scene (Empty)` → `Assets/_Project/Scenes/Lobby.unity`로 저장.

아래 계층대로 GameObject를 생성:

```
[Managers]                       ← Empty GameObject
  └─ NetworkGameManager          ← NetworkGameManager 컴포넌트 부착 (Game 씬에서 이동 또는 새로 생성)

[UI]                             ← Canvas 컴포넌트, Canvas Scaler (Scale With Screen Size, 1080×1920)
  └─ EventSystem                 ← 자동 생성 또는 수동 추가

  └─ LobbyRoot                   ← Empty, LobbyRootView 컴포넌트 부착
       ├─ TopBar                  ← Empty (추후 확장용, 지금은 비워둠)
       ├─ TabBar                  ← Empty, TabBarView 컴포넌트 부착
       │    ├─ BattleTabBtn       ← Button + TextMeshProUGUI ("전투")
       │    ├─ ShopTabBtn         ← Button + TextMeshProUGUI ("상점")
       │    ├─ ProfileTabBtn      ← Button + TextMeshProUGUI ("프로필")
       │    └─ RankingTabBtn      ← Button + TextMeshProUGUI ("랭킹")
       └─ ContentArea             ← Empty (탭 콘텐츠 영역)
            ├─ BattlePanel        ← Empty, BattleRootView 부착
            │    ├─ BattleMainPanel    ← Empty, BattleMainView 부착
            │    │    ├─ SingleplayBtn ← Button + TMP ("싱글플레이")
            │    │    ├─ CustomBtn     ← Button + TMP ("커스텀 게임")
            │    │    └─ RandomBtn     ← Button + TMP ("랜덤 매칭")
            │    ├─ CustomGamePanel    ← Empty, CustomGameView 부착
            │    │    ├─ CreateRoomBtn ← Button + TMP ("방 만들기")
            │    │    ├─ JoinByCodeBtn ← Button + TMP ("코드로 참가")
            │    │    └─ BackBtn       ← Button + TMP ("뒤로")
            │    ├─ CustomHostPanel    ← Empty, CustomHostView 부착
            │    │    ├─ CodeText      ← TextMeshProUGUI ("---")
            │    │    ├─ PlayersText   ← TextMeshProUGUI ("0/2명 연결됨")
            │    │    ├─ StatusText    ← TextMeshProUGUI ("방 생성 중...")
            │    │    ├─ ErrorText     ← TextMeshProUGUI (기본 비활성화)
            │    │    └─ CancelBtn     ← Button + TMP ("취소")
            │    ├─ CustomJoinPanel    ← Empty, CustomJoinView 부착
            │    │    ├─ CodeInput     ← TMP_InputField (PlaceHolder: "코드 입력")
            │    │    ├─ ErrorText     ← TextMeshProUGUI (기본 비활성화)
            │    │    ├─ StatusText    ← TextMeshProUGUI ("")
            │    │    ├─ JoinBtn       ← Button + TMP ("참가")
            │    │    └─ BackBtn       ← Button + TMP ("뒤로")
            │    └─ RandomMatchPanel   ← Empty, RandomMatchView 부착
            │         ├─ StatusText    ← TextMeshProUGUI ("랜덤 매칭 (추후 구현 예정)")
            │         ├─ CancelBtn     ← Button + TMP ("취소")
            │         └─ BackBtn       ← Button + TMP ("뒤로")
            ├─ ShopPanel          ← Empty, ShopView 부착 (TextMeshProUGUI "상점 (준비 중)")
            ├─ ProfilePanel       ← Empty, ProfileView 부착 (TextMeshProUGUI "프로필 (준비 중)")
            └─ RankingPanel       ← Empty, RankingView 부착 (TextMeshProUGUI "랭킹 (준비 중)")
```

**초기 활성화 상태:**
- BattlePanel: **Active** (기본 탭)
- BattleMainPanel: **Active** (기본 화면)
- CustomGamePanel: **Inactive**
- CustomHostPanel: **Inactive**
- CustomJoinPanel: **Inactive**
- RandomMatchPanel: **Inactive**
- ShopPanel: **Inactive**
- ProfilePanel: **Inactive**
- RankingPanel: **Inactive**
- ErrorText (CustomHostPanel, CustomJoinPanel): **Inactive**

---

### STEP 2 — LobbyRoot (LobbyRootView) 컴포넌트 연결

`LobbyRoot` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Tab Bar View | `TabBar` 오브젝트 |
| Battle Root View | `BattlePanel` 오브젝트 |
| Battle Panel | `BattlePanel` 오브젝트 |
| Shop Panel | `ShopPanel` 오브젝트 |
| Profile Panel | `ProfilePanel` 오브젝트 |
| Ranking Panel | `RankingPanel` 오브젝트 |
| Network Game Manager | `[Managers]/NetworkGameManager` 오브젝트 (비워두면 자동 탐색) |

---

### STEP 3 — TabBar (TabBarView) 컴포넌트 연결

`TabBar` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Battle Tab Button | `TabBar/BattleTabBtn` (Button 컴포넌트) |
| Shop Tab Button | `TabBar/ShopTabBtn` |
| Profile Tab Button | `TabBar/ProfileTabBtn` |
| Ranking Tab Button | `TabBar/RankingTabBtn` |
| Normal Color | 기본값 유지 (회색 `#B2B2B2`) |
| Selected Color | 기본값 유지 (흰색 `#FFFFFF`) |

---

### STEP 4 — BattlePanel (BattleRootView) 컴포넌트 연결

`BattlePanel` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Battle Main View | `BattleMainPanel` 오브젝트 |
| Custom Game View | `CustomGamePanel` 오브젝트 |
| Custom Host View | `CustomHostPanel` 오브젝트 |
| Custom Join View | `CustomJoinPanel` 오브젝트 |
| Random Match View | `RandomMatchPanel` 오브젝트 |

---

### STEP 5 — BattleMainPanel (BattleMainView) 컴포넌트 연결

`BattleMainPanel` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Singleplay Button | `BattleMainPanel/SingleplayBtn` |
| Custom Game Button | `BattleMainPanel/CustomBtn` |
| Random Match Button | `BattleMainPanel/RandomBtn` |

---

### STEP 6 — CustomGamePanel (CustomGameView) 컴포넌트 연결

`CustomGamePanel` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Create Room Button | `CustomGamePanel/CreateRoomBtn` |
| Join By Code Button | `CustomGamePanel/JoinByCodeBtn` |
| Back Button | `CustomGamePanel/BackBtn` |

---

### STEP 7 — CustomHostPanel (CustomHostView) 컴포넌트 연결

`CustomHostPanel` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Code Text | `CustomHostPanel/CodeText` (TextMeshProUGUI) |
| Connected Players Text | `CustomHostPanel/PlayersText` |
| Status Text | `CustomHostPanel/StatusText` |
| Error Text | `CustomHostPanel/ErrorText` |
| Cancel Button | `CustomHostPanel/CancelBtn` |

---

### STEP 8 — CustomJoinPanel (CustomJoinView) 컴포넌트 연결

`CustomJoinPanel` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Code Input | `CustomJoinPanel/CodeInput` (TMP_InputField) |
| Error Text | `CustomJoinPanel/ErrorText` |
| Status Text | `CustomJoinPanel/StatusText` |
| Join Button | `CustomJoinPanel/JoinBtn` |
| Back Button | `CustomJoinPanel/BackBtn` |

---

### STEP 9 — RandomMatchPanel (RandomMatchView) 컴포넌트 연결

`RandomMatchPanel` 오브젝트 선택 → Inspector:

| 필드 | 연결 대상 |
|------|---------|
| Status Text | `RandomMatchPanel/StatusText` |
| Cancel Button | `RandomMatchPanel/CancelBtn` |
| Back Button | `RandomMatchPanel/BackBtn` |

---

### STEP 10 — Build Settings 등록

`File → Build Settings`:

| Index | 씬 경로 |
|-------|---------|
| 0 | `Assets/_Project/Scenes/Lobby.unity` |
| 1 | `Assets/_Project/Scenes/Game.unity` |

Lobby.unity를 Index 0으로 올려야 앱 시작 시 로비가 먼저 열림.

---

### STEP 11 — Game.unity 정리

Game 씬에서:
1. `NetworkManager` 오브젝트 → **삭제** (Lobby 씬에서 관리)
2. 기존 `LobbyUI` 오브젝트 → **비활성화 또는 삭제**
3. `GameEndUI` → "로비로 돌아가기" 버튼 오브젝트 **추가** (코드는 이미 구현됨)

---

## 테스트 체크리스트

- [ ] 앱 시작 → Lobby 씬, 전투 탭, BattleMainView 표시
- [ ] 탭 전환 (전투→상점→프로필→랭킹) ReactiveProperty 기반으로 동작
- [ ] 싱글플레이 → Game 씬 즉시 진입, 맵 로드 정상
- [ ] 커스텀 게임 호스트 → 코드 표시, ConnectedPlayers 카운트 업데이트
- [ ] 커스텀 게임 참가 → 코드 입력, 양쪽 Game 씬 동시 전환
- [ ] 에러 발생 시 ErrorMessage View에 표시됨
- [ ] 게임 종료 → "로비로" → Lobby 씬 복귀
- [ ] ViewModel Dispose 시 이벤트 구독 해제 확인 (메모리 누수 없음)
- [ ] Game 씬 기존 기능 (전투, 생산, 건물) 모두 정상
