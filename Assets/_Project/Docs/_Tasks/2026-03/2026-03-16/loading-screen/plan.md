# Plan: 전역 로딩 스크린 구현

**날짜:** 2026-03-16 (수정: 2026-03-17)

---

## 구현 목표

모든 씬 전환 구간(랜덤매칭, 커스텀게임, 싱글플레이)에서 동일한 로딩 스크린 사용.
로딩 스크린은 기존 모든 화면을 덮도록 전체 화면 페이드 인.
전환 소요 시간의 차이만 있을 뿐, UI 구성과 동작 방식은 동일.

---

## 수정/생성 파일

| 파일 | 작업 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/Common/LoadingScreen.cs` | 신규 생성 |
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` | 수정 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs` | 수정 |
| Unity Editor | 프리팹 + Canvas 세팅 (에디터 작업) |

---

## 구현 1: LoadingScreen.cs (신규)

### UI 구조 (프리팹)

```
LoadingScreen (Canvas, Screen Space - Overlay, Sort Order: 999)
└── Root Panel (Image, 전체 화면 검정, CanvasGroup — 페이드용)
    ├── Spinner (Image, 화면 중앙, 회전 애니메이션)
    └── StatusText (TextMeshProUGUI, 스피너 하단)
```

- **Root Panel**: 앵커 stretch-stretch (전체 화면), 검정 단색 불투명 → 기존 화면 완전 차단
- **Sort Order 999**: 게임 내 모든 Canvas 위에 표시 보장
- **CanvasGroup.alpha**: 0→1 페이드 인 (Show), 1→0 페이드 아웃 (Hide)

### 코드 설계

```csharp
public class LoadingScreen : MonoBehaviour
{
    // ── 싱글턴 ──────────────────────────────────────────────
    public static LoadingScreen Instance { get; private set; }

    // ── Inspector ───────────────────────────────────────────
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private RectTransform _spinner;

    [Header("애니메이션")]
    [SerializeField] private float _fadeDuration = 0.3f;
    [SerializeField] private float _spinSpeed = 180f;   // 초당 회전 각도

    // ── 공개 API ────────────────────────────────────────────

    /// <summary>로딩 스크린을 전체 화면으로 페이드 인하며 표시.</summary>
    public void Show(string message = "로딩 중...");

    /// <summary>로딩 스크린을 페이드 아웃하며 숨김.</summary>
    public void Hide();

    // ── 내부 ────────────────────────────────────────────────
    // Awake: 싱글턴 설정 + DontDestroyOnLoad + 초기 alpha=0, interactable=false
    // OnEnable: SceneManager.sceneLoaded += OnSceneLoaded
    // OnDisable: SceneManager.sceneLoaded -= OnSceneLoaded
    // OnSceneLoaded: Hide() 자동 호출 (씬 전환 완료 시 자동 숨김)
    // Update: _spinner.Rotate(Vector3.forward, -spinSpeed * Time.deltaTime)
}
```

### 초기화 방법

`Resources/LoadingScreen` 프리팹으로 저장.
`GameBootstrapper.Awake()` 또는 첫 `Show()` 호출 시 Lazy 초기화:
```csharp
if (Instance == null)
    Instantiate(Resources.Load<GameObject>("LoadingScreen"));
```

---

## 구현 2: 씬 전환별 Show() 호출 위치

### 모든 씬 전환 공통 패턴

```csharp
LoadingScreen.Instance.Show("...");
// 씬 로드 → sceneLoaded 이벤트 → 자동 Hide()
```

### 2-1. 랜덤 매칭 — BattleViewModel.cs + NetworkGameManager.cs

`NetworkGameManager.StartMatchmakingAsync`에 `onMatchFound` 콜백 추가:

```csharp
// NetworkGameManager.cs
public async Task StartMatchmakingAsync(
    Action<int> onWaitSecond = null,
    Action onMatchFound = null)
{
    var matchId = await _matchmakerManager.PollUntilMatchedAsync(...);
    onMatchFound?.Invoke();   // ← matchId 확보 직후
    bool isHost = await _matchmakerManager.DetermineIsHostAsync(matchId);
    ...
}

// BattleViewModel.cs
await _networkManager.StartMatchmakingAsync(
    onWaitSecond: sec => MatchWaitSeconds.Value = sec,
    onMatchFound: () =>
    {
        IsMatchmaking.Value = false;
        LoadingScreen.Instance.Show("게임에 접속하는 중...");
    });
```

**흐름:**
1. 매칭 대기 중 → 기존 타이머 UI 유지
2. matchId 확보 → 로딩 스크린 페이드 인 (기존 RandomMatchView 완전 차단)
3. 씬 로드 완료 → 자동 페이드 아웃

### 2-2. 커스텀 호스트 — BattleViewModel.CmdHostGame()

```csharp
LoadingScreen.Instance.Show("게임 방을 만드는 중...");
await _networkManager.HostGameAsync(...);
// 씬 로드 완료 → 자동 Hide()
// 에러 시 catch → LoadingScreen.Instance.Hide()
```

### 2-3. 커스텀 참가 — BattleViewModel.CmdJoinGame()

```csharp
LoadingScreen.Instance.Show("게임에 참가하는 중...");
await _networkManager.JoinGameAsync(...);
// 씬 로드 완료 → 자동 Hide()
// 에러 시 catch → LoadingScreen.Instance.Hide()
```

### 2-4. 싱글플레이 — BattleViewModel.CmdStartSinglePlay()

```csharp
LoadingScreen.Instance.Show("게임 로딩 중...");
SceneManager.LoadScene("Game");
// 씬 로드 완료 → 자동 Hide()
```

---

## 위험 요소

| 위험 | 평가 | 대응 |
|------|------|------|
| 씬 로드 없이 에러 발생 시 영구 표시 | 낮음 | 모든 catch 블록에서 `LoadingScreen.Instance.Hide()` 호출 |
| DontDestroyOnLoad 중복 인스턴스 | 낮음 | `Awake`에서 `Instance != null`이면 `Destroy(gameObject)` |
| NGO SceneManager.LoadScene 사용 시 sceneLoaded 미발동 가능성 | 확인 필요 | NGO 씬 전환 완료 콜백 별도 확인 후 대응 |
| `Resources.Load` 누락 시 null 참조 | 낮음 | 프리팹 경로 고정 (`Resources/LoadingScreen`) |

---

## 테스트 체크리스트

- [x] 랜덤 매칭 완료 시 로딩 스크린이 기존 UI를 완전히 덮으며 페이드 인 확인 (2026-03-17)
- [x] 커스텀 호스트 시작 시 로딩 스크린 표시 확인 (2026-03-17)
- [x] 커스텀 참가 시작 시 로딩 스크린 표시 확인 (2026-03-17)
- [x] 싱글플레이 시작 시 로딩 스크린 표시 확인 — 2초 딜레이 후 씬 전환 (2026-03-17)
- [x] 게임 씬 진입 후 로딩 스크린 자동 숨김 확인 (2026-03-17)
- [ ] 에러 발생 시 로딩 스크린 숨김 + 에러 메시지 표시 확인
- [ ] 매칭 취소 시 로딩 스크린 미표시 확인
- [ ] 반복 매칭 시 중복 인스턴스 없음 확인
- [ ] Sort Order 999로 모든 Canvas 위에 표시되는지 확인
