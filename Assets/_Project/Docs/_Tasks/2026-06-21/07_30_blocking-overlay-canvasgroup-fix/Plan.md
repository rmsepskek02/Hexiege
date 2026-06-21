# Plan: 반투명 배경 오버레이 UIManager 통합 및 SafeArea 문제 해결

## 작업 목적

모든 UI의 반투명 배경 오버레이(BlockingOverlay, SharedBackground 등)를 **UIManager가 단일 소유**하는 구조로 통합한다.
현재 각 팝업이 개별적으로 배경 오버레이를 소유하여 SafeArea 안에 갇혀 있는 구조적 문제를 근본적으로 해결하고,
UIManager가 제공하는 단일 API를 통해 Modal / Popup 두 모드를 일관되게 처리한다.
아울러 `GameSystemRules_UI.md`에 이 구조를 규칙으로 명문화하여 앞으로 새 UI도 동일한 방식을 따르도록 한다.

---

## 설계 방향

### UIManager가 소유하는 단일 공유 BlockingOverlay

```
UIManager Canvas (SortingOrder=100, DontDestroyOnLoad)
  ├─ BlockingOverlay  ← NEW: Canvas 직속, SafeArea 영향 없음, 전체화면 커버
  └─ SafeAreaContainer
       ├─ ConfirmPopup   ← _blockingOverlay 필드 제거
       └─ LoadingIndicator
```

### UIManager API (2가지 모드)

| 모드 | 호출 방식 | 동작 | 사용 대상 |
|------|---------|------|---------|
| **Modal** | `ShowBlockingOverlay()` (콜백 없음) | 터치해도 아무 일 없음 — 입력만 차단 | ConfirmPopup, AnonymousWarningPopup, RematchRequestPopup |
| **Popup** | `ShowBlockingOverlay(() => Close())` (콜백 있음) | 터치 시 등록된 콜백 실행(= 팝업 닫기) | InGameSettingsUI, BuildingPlacementUI, ProductionPanelUI |

```csharp
// IUIManager 추가 메서드
void ShowBlockingOverlay(System.Action onTap = null); // null = Modal, Action = Popup
void HideBlockingOverlay();
```

---

## 수정 항목

**근거**: GameSystemRules_UI.md **규칙 4** — 전체화면을 채워야 하는 요소는 SafeAreaContainer 밖에 배치한다.
**근거**: GameSystemRules_UI.md **규칙 5** — UI 표시/숨김은 SetActive 대신 CanvasGroup으로 제어한다.

---

### [1] `IUIManager.cs` — 인터페이스 추가

파일: `Assets/_Project/Scripts/Presentation/UI/Core/IUIManager.cs`

```csharp
void ShowBlockingOverlay(System.Action onTap = null);
void HideBlockingOverlay();
```

---

### [2] `UIManager.cs` — BlockingOverlay 소유 및 API 구현

파일: `Assets/_Project/Scripts/Presentation/UI/UIManager.cs`

- `[SerializeField] private CanvasGroup _blockingOverlay` 필드 추가
- `ShowBlockingOverlay(Action onTap = null)` 구현
  - `_blockingOverlay.alpha = 1f / blocksRaycasts = true / interactable = true`
  - `onTap`이 있으면 Button 컴포넌트에 콜백 등록 (Popup 모드)
  - `onTap`이 null이면 Button 없이 CanvasGroup만 활성화 (Modal 모드)
- `HideBlockingOverlay()` 구현
  - `alpha = 0 / blocksRaycasts = false / interactable = false`
  - 등록된 콜백 해제
- `Awake()`에서 BlockingOverlay 초기 숨김 처리

> BlockingOverlay GameObject는 UIManager Canvas 직속 (SafeAreaContainer 바깥)에 배치한다.
> RectTransform: anchorMin=(0,0), anchorMax=(1,1), offset=(0,0) — 전체화면 커버.
> Image raycastTarget=true (Modal 입력 차단 역할).

---

### [3] `ConfirmPopup.cs` — 자체 BlockingOverlay 제거, UIManager 호출로 교체

파일: `Assets/_Project/Scripts/Presentation/UI/ConfirmPopup.cs`

- `_blockingOverlay: CanvasGroup` 필드 **비활성화(주석 처리)** (테스트 통과 후 삭제)
- `Show()` 내부: `UIManager.Instance?.ShowBlockingOverlay()` (Modal 모드)
- `Hide()` 내부: `UIManager.Instance?.HideBlockingOverlay()`

---

### [4] `AnonymousWarningPopup.cs` — 동일 처리

파일: `Assets/_Project/Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs`

- `_blockingOverlay: CanvasGroup` 필드 **비활성화(주석 처리)**
- `Show()`: `UIManager.Instance?.ShowBlockingOverlay()` (Modal 모드)
- `Hide()`: `UIManager.Instance?.HideBlockingOverlay()`

---

### [5] `RematchRequestPopup.cs` — 자체 Overlay 제거, UIManager 호출로 교체

파일: `Assets/_Project/Scripts/Presentation/UI/Common/RematchRequestPopup.cs`

- `_overlay: GameObject` 필드 **비활성화(주석 처리)**
- `_overlayCg: CanvasGroup`, `_overlayFade: Tween` **비활성화(주석 처리)**
- `ShowRequest()`, `ShowDeclined()` 내부: `UIManager.Instance?.ShowBlockingOverlay()` (Modal 모드)
- `Hide()` 내부: `UIManager.Instance?.HideBlockingOverlay()`
- `FadeIn(_overlay, ...)` / `FadeOut(_overlay, ...)` 오버레이 관련 호출 제거

---

### [6] `InGameSettingsUI.cs` — SharedBackgroundButton 제거, UIManager 호출로 교체

파일: `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs`

- `_sharedBackground: SharedBackgroundButton` 필드 **비활성화(주석 처리)**
- `Show()`: `UIManager.Instance?.ShowBlockingOverlay(() => Hide())` (Popup 모드)
- `Hide()`: `UIManager.Instance?.HideBlockingOverlay()`

---

### [7] `BuildingPlacementUI.cs` — 동일 처리

파일: `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

- `_sharedBackground: SharedBackgroundButton` 필드 **비활성화(주석 처리)**
- `Open()`/`Show()`: `UIManager.Instance?.ShowBlockingOverlay(() => Close())` (Popup 모드)
- `Close()`: `UIManager.Instance?.HideBlockingOverlay()`

---

### [8] `BuildingPanelBase.cs` — 동일 처리 (BuildingPlacementUI 상위 클래스일 경우)

파일: `Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs`

- SharedBackgroundButton 관련 로직 확인 후 동일하게 UIManager 호출로 교체

---

### [9] `GameSystemRules_UI.md` — 규칙 업데이트

파일: `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md`

- **규칙 4 (SafeArea)** 항목에 아래 내용 추가:
  > "반투명 배경 오버레이(BlockingOverlay)도 전체화면 배경과 동일하게 SafeAreaContainer 밖에 두어야 한다.
  > 팝업 컴포넌트가 직접 소유하지 않고, UIManager.ShowBlockingOverlay()를 통해 제어한다."
- **규칙 5 (CanvasGroup)** 아래 또는 새 규칙으로 BlockingOverlay 단일 소유 패턴 명문화:
  > "반투명 배경은 UIManager가 단일 소유한다. Modal 모드(터치 차단만)와 Popup 모드(터치 시 닫기)로 구분한다."

---

### [10] 씬 수정 — 에디터 스크립트 자동화

파일: `Assets/Editor/Setup/MigrateBlockingOverlayToUIManager.cs`
메뉴: `Hexiege > Setup > BlockingOverlay UIManager 씬 마이그레이션 실행`

**[Login.unity] 자동 처리:**
- `UIManager Canvas` 직속에 `BlockingOverlay` GameObject 생성
  - Image(color=0,0,0,0.6 / raycastTarget=true) + Button + CanvasGroup(alpha=0 / blocksRaycasts=false / interactable=false)
  - RectTransform: anchorMin=(0,0), anchorMax=(1,1), offset=(0,0)
  - sibling index=0 (SafeAreaContainer보다 먼저 렌더링)
- UIManager 컴포넌트의 `_blockingOverlay`(CanvasGroup) / `_blockingOverlayButton`(Button) 필드 자동 연결
- 씬 자동 저장

**[Game.unity] 자동 처리:**
- `RematchRequestPopup > Overlay` Image의 `raycastTarget = false` 설정
  (코드에서 참조 제거됨 — 터치 차단 역할 불필요)
- 씬 자동 저장

**[확인 필요 — 자동화 미포함]:**
- `Canvas > Background` (SharedBackgroundButton 부착): 이미 `m_IsActive: 0` 확인됨 → 별도 처리 불필요
- Lobby.unity: `GameUIManager`만 존재, `UIManager`는 DontDestroyOnLoad로 Login에서 인계 → 별도 처리 불필요

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| ConfirmPopup 중첩 | 이미 ShowBlockingOverlay 된 상태에서 다시 Show 호출 | 참조 카운터 또는 스택으로 중첩 횟수 관리 — Hide 시 마지막 숫자가 0이 될 때만 실제 숨김 |
| RematchRequestPopup 페이드 | 기존에 DOTween으로 Overlay를 페이드 인/아웃 | UIManager의 HideBlockingOverlay는 즉시 처리, 필요 시 DOFade 래핑 |
| UIManager null (씬 직접 진입) | Lobby/Game 씬 직접 실행 시 UIManager.Instance=null | 호출부 기존 null-safe 패턴 `UIManager.Instance?.ShowBlockingOverlay()` 그대로 유지 |
| SharedBackgroundButton 제거 | 기존 참조 연결이 씬에 남아 있을 수 있음 | 에디터 스크립트로 참조 해제 후 씬 저장, 컴포넌트는 테스트 통과 후 제거 |

---

## 기존 로직 제거 계획

**테스트 통과 전**: 주석 처리(비활성화)로 보존  
**테스트 통과 후 삭제 대상**:
- `ConfirmPopup._blockingOverlay` 필드 및 null 가드
- `AnonymousWarningPopup._blockingOverlay` 필드 및 null 가드
- `RematchRequestPopup._overlay`, `_overlayCg`, `_overlayFade` 및 관련 FadeIn/FadeOut 오버레이 호출
- `InGameSettingsUI._sharedBackground`, `BuildingPlacementUI._sharedBackground` 필드
- `SharedBackgroundButton.cs` 파일 (완전 대체 후)
- Login.unity `BlockingOverlay` GameObjects
- Game.unity `Overlay` GameObject (RematchRequestPopup 하위), `Background` GameObject
