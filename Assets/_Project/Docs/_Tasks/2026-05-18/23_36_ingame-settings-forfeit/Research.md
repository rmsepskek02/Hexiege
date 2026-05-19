# Research — 인게임 설정 메뉴 + 게임 포기 기능

## 이 작업이 하는 일

게임이 진행 중인 화면에서 언제든 설정 메뉴를 열 수 있는 버튼을 HUD에 추가합니다.  
설정 메뉴 안에는 사운드 버튼(아직 기능 없음, 자리만)과 게임 포기 버튼이 있습니다.  
포기 버튼을 누르면 "정말 포기하시겠습니까?"라는 확인창이 뜨고, 수락하면 해당 플레이어는 패배 처리되고 상대방은 즉시 승리 처리됩니다.  
포기 이후에도 양쪽 모두 재경기 신청은 가능합니다.  
아울러 HUD 내 골드·인구·타일 정보를 좌측 상단 1열 4행으로 재배치하고, 우측 상단에 설정 버튼을 배치합니다.

---

## 기존 시스템 분석

### 1. UI 시스템

**AnimatedPanel.cs**
- Show/Hide 애니메이션 처리 컴포넌트
- `AnimationType`: PopupFade(DOFade+DOScale), SlideFromBottom, SlideFromTop
- `SetUpdate(true)` 적용 → `Time.timeScale=0` 상태에서도 애니메이션 동작
- Hide() 완료 후 해당 GameObject를 `SetActive(false)` 처리

**SharedBackgroundButton.cs (`Presentation/UI/Common/`)**
- 공유 배경 GameObject에 부착
- `Register(Action)`: 팝업 Show 시 호출 → 배경 클릭 시 등록된 Close() 실행
- `Unregister()`: 팝업 Close 시 호출 → 배경 클릭 무효화
- 두 번째 패널이 Register하면 첫 번째 콜백 덮어씀 (한 번에 하나만 활성)

**공유 Background 구조**
- `[UI]/Background` 하나를 ProductionPopup, BuildingPopup, GameEndPanel이 공유
- Canvas 내 첫 번째 자식 → 다른 패널들보다 렌더링 우선순위 낮음 (항상 패널 뒤에 표시)
- `GraphicRaycaster` 히트 대상 → 활성 상태에서 터치/클릭을 받아 InputHandler 게임 입력 자동 차단
- AnimatedPanel의 `_backgroundOverlay(CanvasGroup)` 필드에 연결 → Show 시 즉시 SetActive(true), Hide 완료 시 SetActive(false)

**GameUIManager.cs + IGameUI.cs**
- `IGameUI`: OnGameStarted(), OnGameEnded(), OnGamePaused(), OnGameResumed() — 모두 default 빈 구현
- `GameUIManager.Register(IGameUI)`: GameBootstrapper.LoadMap() 앞부분에서 등록
- 게임 종료 시 `NotifyGameEnded()` → 등록된 모든 IGameUI에 OnGameEnded() 전달

### 2. 현재 HUD (GameHudUI.cs)

현재 Inspector 필드:
- `_goldText`, `_populationText`, `_blueTileCountText`, `_redTileCountText`
- `Initialize(ResourceUseCase, PopulationUseCase)` — GameBootstrapper에서 호출

이번 작업에서 추가:
- `_settingsButton (Button)` — 설정 버튼
- `_settingsUI (InGameSettingsUI)` — 버튼 클릭 시 Show() 호출 대상

### 3. 입력 차단 방식

`InputHandler.HandleClick()` 첫 단계:
```
IsPointerOverUI(screenPos)
  → EventSystem.RaycastAll
  → results.Count > 0 이면 게임 입력 무시
```
공유 Background가 `SetActive(true)`가 되면 Background의 Image가 RaycastAll에 히트 → InputHandler가 게임 입력 차단.

**결론**: 별도 플래그 없이 공유 Background 활성화만으로 타일 클릭 등 게임 입력이 자동 차단됨.

ConfirmPopup은 InGameSettingsPanel 위(Canvas 형제 순서 마지막)에 위치하며,  
자체 `BlockingOverlay(투명 Image, Raycast Target=true)`로 ConfirmPopup 뒤의 클릭을 차단.  
덕분에 ConfirmPopup이 열린 동안 공유 Background를 클릭해도 InGameSettingsPanel이 닫히지 않음.

### 4. 게임 종료 흐름

**싱글플레이:**
```
GameEndUseCase.OnBuildingDied
  → IsGameOver=true
  → GameEvents.OnGameEnd.OnNext
  → GameEndUI.OnGameEnd → Time.timeScale=0 + 결과 패널 표시
```

**멀티플레이:**
```
NetworkCombatController → NetworkGameEndController.OnGameEndServer (서버)
  → AnnounceWinnerClientRpc(winnerTeamIndex, isRandomMatch)
  → GameEndUI.ShowResult + SetupRematchButton (모든 클라이언트)
```

**포기 시 추가 흐름:**
- 싱글플레이: `GameEndUseCase.Forfeit()` 메서드 추가 → IsGameOver=true + OnGameEnd 직접 발행
- 멀티플레이: `NetworkGameEndController.ForfeitServerRpc` 추가 → 기존 AnnounceWinnerClientRpc 재사용

`NetworkGameEndController._announced(bool)` 플래그가 이미 있어 포기와 일반 게임오버의 중복 처리를 방지 가능.

### 5. 재경기 시스템

포기 후에도 재경기 가능: 기존 `RequestRematchServerRpc` → `AnnounceWinnerClientRpc(isRandomMatch=false)` → `SetupRematchButton` 흐름 그대로 사용.  
별도 처리 불필요.

### 6. 싱글플레이 TimeScale 처리

- 설정 메뉴 열릴 때: `Time.timeScale = 0` (싱글플레이만)
- 설정 메뉴 닫힐 때: `Time.timeScale = 1` 복원 (싱글플레이만, `_pausedBySettings` 플래그로 추적)
- 포기 확정 후: `GameEndUI.OnGameEnd`가 `Time.timeScale = 0` 재설정 → 충돌 없음
- AnimatedPanel의 DOTween은 `SetUpdate(true)` 적용 → timeScale=0에서도 정상 동작

---

## 수정/신규 파일 목록

| 구분 | 파일 | 변경 내용 |
|------|------|----------|
| 수정 | `Presentation/UI/GameHudUI.cs` | 설정 버튼 필드 추가, InGameSettingsUI 참조 추가 |
| 수정 | `Application/UseCases/GameEndUseCase.cs` | Forfeit() public 메서드 추가 |
| 수정 | `Infrastructure/Network/NetworkGameEndController.cs` | RequestForfeit() + ForfeitServerRpc 추가 |
| 수정 | `Bootstrap/GameBootstrapper.cs` | 신규 UI Inspector 참조 + LoadMap 등록 추가 |
| 신규 | `Presentation/UI/InGameSettingsUI.cs` | 설정 메뉴 팝업 (IGameUI 구현) |
| 신규 | `Presentation/UI/ConfirmPopup.cs` | 범용 확인 팝업 (재활용 가능) |

---

## Inspector 작업 (씬 구조 변경)

**Game.unity `[UI] Canvas` 구조 변경:**
```
[UI] Canvas
  ├─ Background (기존 공유 배경 — 위치 변경 없음)
  ├─ GameHUD (변경)
  │   ├─ StatsPanel (좌측 상단, 1열 4행 세로 배치)
  │   │   ├─ GoldText
  │   │   ├─ PopulationText
  │   │   ├─ BlueTileCountText
  │   │   └─ RedTileCountText
  │   └─ SettingsButton (우측 상단)
  ├─ [기존 팝업들 — 변경 없음]
  ├─ InGameSettingsPanel  ← 신규
  │   └─ Panel (AnimatedPanel - PopupFade)
  │       ├─ CloseButton (X)
  │       ├─ SoundButton (미구현 플레이스홀더)
  │       └─ ForfeitButton
  └─ ConfirmPopup  ← 신규 (Canvas 마지막 자식 — 최상위)
      ├─ BlockingOverlay (투명 Image, Raycast Target=true, 전체 화면)
      └─ Panel (AnimatedPanel - PopupFade)
          ├─ MessageText
          ├─ ConfirmButton
          └─ CancelButton
```

**신규 Inspector 연결 필요:**
- `GameBootstrapper` → `_inGameSettingsUI`, `_confirmPopup`
- `GameHudUI` → `_settingsButton`, `_settingsUI`
- `InGameSettingsUI` → `_sharedBackground`, `_confirmPopup`, `_panel(AnimatedPanel)`, 각 버튼
- `ConfirmPopup` → `_blockingOverlay`, `_panel(AnimatedPanel)`, `_messageText`, `_confirmButton`, `_cancelButton`
- `InGameSettingsPanel`의 `AnimatedPanel._backgroundOverlay` → 공유 Background의 CanvasGroup 연결
