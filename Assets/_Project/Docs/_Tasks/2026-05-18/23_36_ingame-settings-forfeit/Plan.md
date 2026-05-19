# Plan — 인게임 설정 메뉴 + 게임 포기 기능

## 이 작업이 하는 일

HUD에 설정 버튼을 추가하고, 설정 메뉴 팝업과 재활용 가능한 확인 팝업을 새로 만듭니다.  
설정 메뉴에서 게임 포기를 선택하면 확인창을 거쳐 해당 플레이어가 패배 처리됩니다.  
싱글플레이에서는 설정 메뉴가 열리면 게임이 일시정지되고, 멀티플레이에서는 계속 진행됩니다.

---

## GameSystemRules.md 검토 결과

게임 포기 및 설정 메뉴에 관한 별도 규칙은 현재 GameSystemRules.md에 없습니다.  
이번 작업에서 신규 설계한 규칙(싱글 일시정지, 재경기 허용 등)을 구현 후 GameSystemRules.md에 추가 기록 권장.

---

## 구현 범위 및 순서

### Step 1. `ConfirmPopup.cs` 신규 생성 (`Presentation/UI/`)

범용 확인 팝업. 어떤 메시지와 콜백이든 주입하여 재활용 가능.

**필드:**
```
[SerializeField] private GameObject _blockingOverlay  // 투명 전체화면 Image, Raycast Target=true
[SerializeField] private AnimatedPanel _panel          // PopupFade 타입
[SerializeField] private TextMeshProUGUI _messageText
[SerializeField] private Button _confirmButton
[SerializeField] private Button _cancelButton
[SerializeField] private TextMeshProUGUI _confirmButtonText
[SerializeField] private TextMeshProUGUI _cancelButtonText
```

**핵심 API:**
```csharp
public void Show(string message, string confirmLabel, string cancelLabel,
                 Action onConfirm, Action onCancel = null)
public void Hide()
```

**Show() 동작:**
1. 텍스트/버튼 라벨 갱신
2. 콜백 저장 (`_onConfirm`, `_onCancel`)
3. 버튼 리스너 등록 (RemoveAllListeners 후 재등록)
4. `_blockingOverlay.SetActive(true)` — 하위 설정 메뉴 배경 클릭 차단
5. `_panel.gameObject.SetActive(true)` → `_panel.Show()`

**Hide() 동작:**
1. `_blockingOverlay.SetActive(false)` — 즉시
2. `_panel.Hide()` — 완료 후 Panel SetActive(false) (AnimatedPanel 내부 처리)

**주의**: AnimatedPanel이 Panel GameObject 자체를 SetActive(false)하므로, 다음 Show()전 `_panel.gameObject.SetActive(true)` 필요.

---

### Step 2. `InGameSettingsUI.cs` 신규 생성 (`Presentation/UI/`)

인게임 설정 메뉴 팝업. `IGameUI` 구현으로 GameUIManager에 등록.

**필드:**
```
[SerializeField] private AnimatedPanel _panel              // PopupFade 타입
[SerializeField] private SharedBackgroundButton _sharedBackground
[SerializeField] private ConfirmPopup _confirmPopup
[SerializeField] private Button _closeButton               // X 버튼
[SerializeField] private Button _soundButton               // 미구현 플레이스홀더
[SerializeField] private Button _forfeitButton
```

**내부 상태:**
```
private bool _pausedBySettings  // 싱글플레이 timeScale=0 여부 추적
private GameEndUseCase _gameEndUseCase  // 싱글플레이 포기용, Initialize로 주입
```

**Initialize(GameEndUseCase gameEndUseCase):**
- GameBootstrapper.LoadMap()에서 호출
- 버튼 리스너 등록
- Hide() 호출로 초기 상태 보장

**Show() 동작:**
1. 싱글플레이(`!NetworkContext.IsNetworkActive`)이면 `Time.timeScale=0`, `_pausedBySettings=true`
2. `_sharedBackground.Register(Hide)` — 배경 클릭 시 설정 메뉴 닫기
3. `_panel.Show()`

**Hide() 동작:**
1. `_confirmPopup?.Hide()` — 열려있을 수 있는 확인 팝업도 함께 닫기
2. `_sharedBackground.Unregister()`
3. if `_pausedBySettings`: `Time.timeScale=1`, `_pausedBySettings=false`
4. `_panel.Hide()`

**포기 버튼 클릭 (OnForfeitClicked):**
```csharp
_confirmPopup.Show(
    message: "정말 포기하시겠습니까?",
    confirmLabel: "포기",
    cancelLabel: "취소",
    onConfirm: OnForfeitConfirmed,
    onCancel: null  // 팝업만 닫힘
);
```

**OnForfeitConfirmed():**
```csharp
if (NetworkContext.IsNetworkActive)
{
    var netGameEnd = FindFirstObjectByType<NetworkGameEndController>();
    netGameEnd?.RequestForfeit();
}
else
{
    _gameEndUseCase?.Forfeit();
}
Hide();  // 설정 메뉴 닫기 (timeScale 복원 포함)
```

**IGameUI 구현:**
- `OnGameStarted()`: Hide() — 재경기 시 설정 메뉴 열려있다면 닫기
- `OnGameEnded()`: Hide() — 게임 종료 시 설정 메뉴 닫기

**주의**: `OnGameEnded()`에서 Hide()를 호출하면 timeScale 복원(`Time.timeScale=1`)이 발생하지만, `GameEndUI.OnGameEnd()`가 이후 `Time.timeScale=0`으로 다시 설정하므로 순서 충돌 없음.

---

### Step 3. `GameEndUseCase.cs` 수정

싱글플레이 포기 처리용 메서드 추가.

**추가 내용:**
```csharp
/// <summary>
/// 싱글플레이 포기. 로컬 플레이어(Blue팀)를 패배 처리.
/// 게임이 이미 종료된 상태라면 무시.
/// </summary>
public void Forfeit()
{
    if (IsGameOver) return;
    IsGameOver = true;
    // 싱글플레이에서 로컬 플레이어는 항상 Blue팀
    // Blue팀 포기 → Red팀 승리
    GameEvents.OnGameEnd.OnNext(new GameEndEvent(TeamId.Red));
}
```

---

### Step 4. `NetworkGameEndController.cs` 수정

멀티플레이 포기 처리용 public 메서드 + ServerRpc 추가.

**추가 내용:**

```csharp
/// <summary>
/// 포기 요청. InGameSettingsUI에서 포기 확정 시 호출.
/// </summary>
public void RequestForfeit()
{
    ForfeitServerRpc();
}

/// <summary>
/// 클라이언트의 포기를 서버에서 처리. 포기자 팀을 패배 처리.
/// Host = ClientId 0 = Blue팀, Client = Red팀.
/// </summary>
[ServerRpc(RequireOwnership = false)]
private void ForfeitServerRpc(ServerRpcParams rpcParams = default)
{
    if (!IsServer) return;
    if (_announced) return;

    ulong forfeiterId = rpcParams.Receive.SenderClientId;
    TeamId forfeitTeam = (forfeiterId == 0) ? TeamId.Blue : TeamId.Red;
    TeamId winnerTeam = (forfeitTeam == TeamId.Blue) ? TeamId.Red : TeamId.Blue;

    _announced = true;
    Debug.Log($"[Network] 포기 처리. 포기자ClientId={forfeiterId}, 포기팀={forfeitTeam}, 승리팀={winnerTeam}");

    // 포기로 인한 종료 — 재경기 신청 가능(isRandomMatch=false)
    AnnounceWinnerClientRpc((int)winnerTeam, false);
}
```

---

### Step 5. `GameHudUI.cs` 수정

설정 버튼 연결.

**추가 필드:**
```csharp
[Header("설정 버튼")]
[SerializeField] private Button _settingsButton;
[SerializeField] private InGameSettingsUI _settingsUI;
```

**Initialize() 내 추가:**
```csharp
if (_settingsButton != null)
{
    _settingsButton.onClick.RemoveListener(OnSettingsClicked);
    _settingsButton.onClick.AddListener(OnSettingsClicked);
}
```

**추가 메서드:**
```csharp
private void OnSettingsClicked()
{
    _settingsUI?.Show();
}
```

---

### Step 6. `GameBootstrapper.cs` 수정

신규 UI 참조 추가 및 LoadMap() 등록.

**Inspector 필드 추가:**
```csharp
[Header("인게임 설정 메뉴")]
[Tooltip("인게임 설정 메뉴 팝업")]
[SerializeField] private InGameSettingsUI _inGameSettingsUI;

[Tooltip("범용 확인 팝업")]
[SerializeField] private ConfirmPopup _confirmPopup;
```

**LoadMap() 앞부분 (UI Register 섹션) 추가:**
```csharp
_uiManager.Register(_inGameSettingsUI);
```

**LoadMap() 내 Initialize 호출 추가:**
```csharp
_inGameSettingsUI?.Initialize(_gameEndUseCase);
```

---

### Step 7. Inspector 작업 (Game.unity) — Editor 스크립트 자동화

1회성 Editor 스크립트(`SetupInGameSettingsUI.cs`) 작성 → 사용자가 Unity 메뉴에서 실행.  
실행 완료 후 해당 스크립트 파일 삭제 가능.

**GameHUD 재배치 (반응형 앵커 적용):**
- `StatsPanel`: 앵커 top-left, VerticalLayoutGroup 자동 배치, 4개 텍스트 1열 4행
- `SettingsButton`: 앵커 top-right

**신규 오브젝트 생성:**
- `InGameSettingsPanel` (Canvas의 끝에서 두 번째 자식)
  - AnimatedPanel(PopupFade), `_backgroundOverlay` → 공유 Background의 CanvasGroup
  - 내부: CloseButton(X, 우측 상단), SoundButton, ForfeitButton
- `ConfirmPopup` (Canvas 마지막 자식 — 최상위)
  - `BlockingOverlay`: Image(alpha=0, Raycast Target=true), 앵커 전체화면(0,0)~(1,1)
  - `Panel`: AnimatedPanel(PopupFade), 내부에 MessageText/ConfirmButton/CancelButton

**Inspector 참조 연결 체크리스트:**
- [ ] `GameBootstrapper._inGameSettingsUI` → InGameSettingsPanel
- [ ] `GameBootstrapper._confirmPopup` → ConfirmPopup
- [ ] `GameHudUI._settingsButton` → SettingsButton
- [ ] `GameHudUI._settingsUI` → InGameSettingsPanel
- [ ] `InGameSettingsUI._sharedBackground` → Background의 SharedBackgroundButton
- [ ] `InGameSettingsUI._confirmPopup` → ConfirmPopup
- [ ] `InGameSettingsUI._panel` → InGameSettingsPanel 내 AnimatedPanel
- [ ] `InGameSettingsUI._closeButton` → CloseButton
- [ ] `InGameSettingsUI._soundButton` → SoundButton
- [ ] `InGameSettingsUI._forfeitButton` → ForfeitButton
- [ ] `AnimatedPanel(InGameSettingsPanel)._backgroundOverlay` → Background CanvasGroup
- [ ] `ConfirmPopup._blockingOverlay` → BlockingOverlay GameObject
- [ ] `ConfirmPopup._panel` → ConfirmPopup 내 AnimatedPanel
- [ ] `ConfirmPopup._messageText` → MessageText
- [ ] `ConfirmPopup._confirmButton` → ConfirmButton
- [ ] `ConfirmPopup._cancelButton` → CancelButton
- [ ] `ConfirmPopup._confirmButtonText` → ConfirmButton 내 TMP
- [ ] `ConfirmPopup._cancelButtonText` → CancelButton 내 TMP

---

## 위험 요소

| 항목 | 위험 | 대응 |
|------|------|------|
| timeScale 복원 타이밍 | OnGameEnded()에서 timeScale=1 복원 후 GameEndUI가 timeScale=0 재설정 | GameEndUI가 이후에 설정하므로 순서상 충돌 없음 |
| _announced 플래그 중복 | 일반 게임오버 직후 포기 RPC 도달 시 이중 처리 | `_announced` 플래그가 이미 true면 ForfeitServerRpc 무시 |
| ConfirmPopup BlockingOverlay | ConfirmPopup 열린 상태에서 배경 클릭 시 InGameSettingsPanel 닫힘 | BlockingOverlay가 전체화면을 덮어 배경 클릭 자체를 차단 |
| 멀티플레이 포기 후 GameEndUseCase.IsGameOver | 클라이언트 측 IsGameOver가 false 상태 유지 (정상) | 멀티플레이에서는 NetworkGameEndController가 권위. IsGameOver 불일치는 무해 |
| AnimatedPanel Hide 후 재Show | Hide() 완료 후 Panel이 SetActive(false) → 다음 Show() 전 SetActive(true) 필요 | ConfirmPopup.Show()에서 `_panel.gameObject.SetActive(true)` 선행 호출로 처리 |

---

## 반응형 UI 제약 (필수)

Canvas Scaler: Scale With Screen Size, Reference Resolution 1080×1920.
**고정 픽셀값(sizeDelta) 및 절대 좌표 사용 금지** — 앵커 기반 배치 원칙 (UIGuidelines.md § 4).

| 오브젝트 | 앵커 설정 | 비고 |
|---------|----------|------|
| BlockingOverlay (ConfirmPopup) | min(0,0) ~ max(1,1), sizeDelta(0,0) | 전체 화면 풀스트레치 |
| InGameSettingsPanel 루트 | min(0,0) ~ max(1,1), sizeDelta(0,0) | 풀스트레치, 내부 Panel은 중앙 앵커 |
| InGameSettingsPanel > Panel | 앵커 center(0.5,0.5), sizeDelta 고정 | 1080×1920 기준 크기. Canvas Scaler가 비례 스케일 |
| ConfirmPopup 루트 | min(0,0) ~ max(1,1), sizeDelta(0,0) | 풀스트레치 |
| ConfirmPopup > Panel | 앵커 center(0.5,0.5), sizeDelta 고정 | 동일 |
| StatsPanel (HUD) | 앵커 top-left, Stretch 없음 | VerticalLayoutGroup으로 4행 자동 배치 |
| SettingsButton (HUD) | 앵커 top-right | 고정 크기 버튼 |

Editor 스크립트에서 UI 오브젝트 생성 시 `anchorMin`, `anchorMax`, `anchoredPosition`, `sizeDelta` 를 위 표 기준으로 설정. `RectTransform`에 절대 픽셀 좌표 직접 기입 금지.

---

## 에셋 사양

| 용도 | 에셋 경로 |
|------|---------|
| 패널 배경 (설정 메뉴, 확인 팝업) | `Sprites/UI/Panels/ui_panel_light.png` |
| 일반 버튼 (포기, 사운드, 확인, X닫기) | `Sprites/UI/Buttons/ui_btn_gold_normal.png` |
| 취소 버튼 | `Sprites/UI/Buttons/ui_btn_cancel.png` |
| HUD 설정 버튼 아이콘 (임시) | `Sprites/UI/Icons/ui_icon_lock.png` |
| 텍스트 색상 | 검은색 (Color.black) |
| 폰트 | `Assets/_Project/Fonts/Maplestory Light SDF.asset` |

---

## 구현 순서 요약

1. `ConfirmPopup.cs` 신규 생성
2. `InGameSettingsUI.cs` 신규 생성
3. `GameEndUseCase.cs` Forfeit() 추가
4. `NetworkGameEndController.cs` RequestForfeit() + ForfeitServerRpc 추가
5. `GameHudUI.cs` 설정 버튼 필드 추가
6. `GameBootstrapper.cs` 필드 + 등록 추가
7. Inspector 작업 (Game.unity 씬 구조 변경 + 참조 연결)
