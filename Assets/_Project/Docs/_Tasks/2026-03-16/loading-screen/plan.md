# Plan: 전역 로딩 스크린 구현

**날짜:** 2026-03-16

---

## 구현 목표

씬 전환·매칭 완료 등 로딩이 필요한 모든 구간에서 재사용 가능한
전역 로딩 스크린 구현.

---

## 수정/생성 파일

| 파일 | 작업 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/Common/LoadingScreen.cs` | 신규 생성 |
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` | 수정 |
| Unity Editor | 프리팹 + Canvas 세팅 (에디터 작업) |

---

## 구현 1: LoadingScreen.cs (신규)

### UI 구조 (프리팹)

```
LoadingScreen (Canvas, Screen Space - Overlay, Sort Order: 100)
└── Root Panel (CanvasGroup — 페이드용)
    ├── Background (Image, 검정 반투명)
    ├── Spinner (Image, 회전 애니메이션)
    └── StatusText (TextMeshProUGUI)
```

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

    /// <summary>로딩 스크린을 페이드 인하며 표시.</summary>
    public void Show(string message = "로딩 중...");

    /// <summary>로딩 스크린을 페이드 아웃하며 숨김.</summary>
    public void Hide();

    // ── 내부 ────────────────────────────────────────────────
    // Awake: 싱글턴 설정 + DontDestroyOnLoad
    // OnEnable: SceneManager.sceneLoaded += OnSceneLoaded 등록
    // OnDisable: SceneManager.sceneLoaded -= OnSceneLoaded 해제
    // OnSceneLoaded: Hide() 자동 호출 (씬 전환 완료 시 자동 숨김)
    // Update: _spinner.Rotate(spinSpeed) (DoTween 불필요, 단순 회전)
}
```

### 초기화 방법

`Resources/LoadingScreen` 프리팹으로 저장 →
`Awake`에서 `Instance == null`이면 `Instantiate(Resources.Load("LoadingScreen"))`.

또는 첫 `Show()` 호출 시 Lazy 초기화.

---

## 구현 2: BattleViewModel.cs 수정

### 변경 내용

`NetworkGameManager.StartMatchmakingAsync`에 `onMatchFound` 콜백 파라미터 추가,
매칭 완료(matchId 확보) 시점에 로딩 스크린 표시.

#### NetworkGameManager.StartMatchmakingAsync 시그니처 변경

```csharp
// 변경 전
public async Task StartMatchmakingAsync(Action<int> onWaitSecond = null)

// 변경 후
public async Task StartMatchmakingAsync(
    Action<int> onWaitSecond = null,
    Action onMatchFound = null)
```

내부 호출:
```csharp
var matchId = await _matchmakerManager.PollUntilMatchedAsync(...);
onMatchFound?.Invoke();   // ← 매칭 완료 직후 콜백
bool isHost = await _matchmakerManager.DetermineIsHostAsync(matchId);
```

#### BattleViewModel.CmdStartMatchmaking 수정

```csharp
await _networkManager.StartMatchmakingAsync(
    onWaitSecond: sec => MatchWaitSeconds.Value = sec,
    onMatchFound: () =>
    {
        IsMatchmaking.Value = false;
        LoadingScreen.Instance.Show("매칭 완료! 게임에 접속합니다...");
    });
```

**흐름:**
1. 매칭 대기 중 → `"매칭 중... 00:XX"` 타이머 표시 (기존 유지)
2. 매칭 완료 → `IsMatchmaking = false` → 취소 버튼 숨김 + 로딩 스크린 표시
3. 씬 로드 완료 → `sceneLoaded` 이벤트 → `LoadingScreen.Hide()` 자동 호출

---

## 재사용 예시 (향후)

```csharp
// 커스텀 게임 씬 전환
LoadingScreen.Instance.Show("게임 로딩 중...");
SceneManager.LoadScene("Game");
// → sceneLoaded 이벤트로 자동 숨김

// 싱글플레이
LoadingScreen.Instance.Show("게임 준비 중...");
SceneManager.LoadScene("Game");
```

---

## 위험 요소

| 위험 | 평가 | 대응 |
|------|------|------|
| 씬 로드 없이 Hide() 미호출 시 영구 표시 | 낮음 (에러 흐름에서 catch로 처리) | `catch` 블록에서 `LoadingScreen.Instance.Hide()` 호출 |
| DontDestroyOnLoad 씬 전환 시 중복 인스턴스 | 낮음 | `Awake`에서 `Instance != null`이면 `Destroy(gameObject)` |
| `Resources.Load` 누락 시 null 참조 | 낮음 | 프리팹 경로 고정 (`Resources/LoadingScreen`) |

---

## 테스트 체크리스트

- [ ] 랜덤 매칭 완료 시 로딩 스크린 표시 확인
- [ ] 게임 씬 진입 후 로딩 스크린 자동 숨김 확인
- [ ] 매칭 취소 시 로딩 스크린 미표시 확인 (취소는 매칭 완료 전)
- [ ] 매칭 중 에러 발생 시 로딩 스크린 미표시 + 에러 메시지 표시 확인
- [ ] 반복 매칭 시 중복 인스턴스 없음 확인
- [ ] Sort Order 100으로 다른 Canvas 위에 올라오는지 확인
