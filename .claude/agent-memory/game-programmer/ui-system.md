# Game Programmer — UI 시스템

전역 UIManager, BlockingOverlay, SceneLoader, Loading Indicator, Canvas SortingOrder, 팝업/패널 패턴.

---

## 전역 UIManager

- `SingletonMonoBehaviour<UIManager>` + `IUIManager`, DontDestroyOnLoad. Login 씬에서 1회 생성, 전 씬 공유
- **반드시 루트 GameObject로 배치** (DontDestroyOnLoad는 루트만 작동. 자식 배치 시 씬 전환마다 재생성+즉시파괴 → UIManager.Instance==null)
- IUIManager API: `ShowConfirm(message, onConfirm, onCancel=null, confirmLabel, cancelLabel)`, `ShowLoading(bool show, string message="")`, `ShowBlockingOverlay(Action onTap=null)` / `HideBlockingOverlay()`, `LoadSceneWithDelay`
- null-safe: 미생성 씬(Lobby/Game) 단독 실행 시 `UIManager.Instance?.` 패턴

---

## BlockingOverlay (UIManager 단일 소유)

- 각 팝업이 개별 소유하던 반투명 배경을 UIManager 단일 소유로 통합 (SafeArea 갇힘 해결)
- `_blockingOverlay`(CanvasGroup) + `_blockingOverlayButton`(Button). Modal(onTap=null, 입력차단만) / Popup(onTap!=null, 터치 시 콜백) 2모드
- **중첩은 `_blockingOverlayRefCount` 참조 카운터** — 0일 때만 실제 숨김. 언더플로 가드(`if(>0)`)
- 패널 전환 시 카운터 누수 주의: RematchRequestPopup 요청→거절 전환 시 `_overlayShown` bool 가드 + ShowOverlayOnce/HideOverlayOnce로 항상 0/1만 점유

---

## SceneLoader

- `Hexiege.Presentation` 정적 유틸리티. 모든 씬 전환 단일 진입점. `UIManager.LoadSceneWithDelay` 위임 (ShowLoading(true) 즉시 → 1초 대기 → LoadScene)
- 상수: `SceneLoader.Lobby` 등
- **Infrastructure→SceneLoader 직접 호출 금지** → GameEvents 경유 (OnNetworkBackToLobby / OnNetworkRematchStarting)

---

## Loading Indicator

- **ShowLoading 호출 위치**: 코루틴 외부에서 동기 실행 필수 (코루틴 내부면 다음 프레임 실행 → 텍스트 지연)
- **ShowLoading(false) 책임자(규칙 L-3)**: Login=LoginBootstrapper, Lobby=LobbyRootView.Start(), Game=GameBootstrapper.LoadMap()
- **초기 메시지 누락 주의**: ShowLoading(true) 메시지 없이 호출하면 텍스트 공백. 반드시 메시지 함께
- 최소 표시 시간(`_loadingMinDuration`, 기본 1f): ShowLoading(false) 시 미경과면 WaitForSecondsRealtime 지연 후 숨김. `_loadingShowTime` 기록 + `_hideLoadingCoroutine` 중복 hide 방지
- 독립 Canvas SortingOrder=300 (다른 팝업에 가리지 않도록)
- **ShowLoading(true)는 씬 전환이 실제로 일어나는 경우에만 사용**: ShowLoading(false)를 호출할 책임자(규칙 L-3)는 씬 전환 후의 Bootstrapper/RootView다. 씬 전환이 없으면 ShowLoading(false)가 호출되지 않아 로딩 인디케이터가 영구히 남는다.
- **포기(Forfeit)는 씬 전환 없이 GameEndUI만 표시 → ShowLoading 불필요**: 2026-06-26 `InGameSettingsUI.OnForfeitConfirmed()`에서 `ShowLoading(true)` 호출 제거(씬 전환 없어 해제 불가). GameSystemRules_UI.md 규칙 L-2 "게임 포기(멀티)" 항목 함께 제거.

---

## Canvas SortingOrder (최종 확정 구조)

```
SO 0   → [UI] Canvas (Game 씬 HUD)
SO 100 → UIManager Canvas (BlockingOverlay)
SO 200 → 각 패널 Canvas Override (BuildingPopup, BuildingActionPanel, InGameSettings, GameEndPanel, ProductionPopup)
SO 250 → ConfirmPopup 독립 Canvas (모달 팝업 — 항상 패널 위)
SO 300 → LoadingIndicator 독립 Canvas
```
- 게임 씬 패널이 UIManager보다 높은 SO 필요 시 Canvas Override 사용 (Override Sorting=true + GraphicRaycaster). GameSystemRules_UI Rule 4
- 참조: `GameSystemRules/GameSystemRules_CanvasSortingOrder.md`

---

## CanvasGroup 패턴 (Rule 5 — SetActive 금지)

- `SetActive(false)` 대신 `alpha=0; blocksRaycasts=false; interactable=false`
- `SetActive(true)` 대신 `alpha=1; blocksRaycasts=true; interactable=true`
- **이유**: SetActive(false)는 LayoutGroup에서 완전 제외(재활성화 시 레이아웃 깨짐), DontDestroyOnLoad에서 Awake 미호출
- 컴포넌트 부착은 런타임 AddComponent가 아닌 에디터에서 미리 (GameSystemRules_UI Rule 5)
- **신규 UI 뷰 체크리스트**: CanvasGroup 부착, `_canvasGroup` 연결, Show/Hide CanvasGroup 패턴

---

## 레이아웃 패턴

### VLG 자식 고정픽셀 → 앵커 기반 전환
- VLG: `childControlHeight=true`, `childForceExpandHeight=false`
- 각 자식 LayoutElement: `preferredHeight=원래SizeDelta.y`, `flexibleHeight=0`, 나머지 -1. 자식 sizeDelta=0
- ⚠️ childForceExpandHeight=true면 버튼이 비정상적으로 커짐
- ⚠️ flexibleHeight>0이면 추가 분배받아 크기 변동

### 균등 분배 (버튼 크기 불일치)
- 슬롯 아이콘 native size가 VLG preferredHeight 배분(Phase2)에서 Row 불균등 유발. childForceExpandHeight는 Phase3만 작동
- 해결: Row에 LayoutElement(preferredHeight=0, flexibleHeight=1), Slot에 (preferredWidth=0, flexibleWidth=1)

### Safe Area 전체화면 배경
- 전체화면 배경은 SafeAreaContainer 밖(Canvas 직속). anchor(0,0)~(1,1), offsetMin=offsetMax=0
- raycastTarget=false 필수. Hierarchy 순서는 SafeAreaContainer보다 위(먼저 그려짐=뒤에 표시)
- GameSystemRules_UI Rule 4

### IgnoreLayout 배지 (Lock Icon 등)
- Slot GO에 HorizontalLayoutGroup 있으면 배지가 가로로 나란히 배치됨 → `LayoutElement.ignoreLayout=true` 필수

---

## ConfirmPopup / 팝업

- ConfirmPopup: 범용 확인 팝업. Show(message, confirmLabel, cancelLabel, onConfirm, onCancel). 독립 Canvas SO=250
- ConfirmPopup.prefab 루트에 자체 Canvas 없으면 부모 UIManager Canvas(SO=100) 따라감 → 패널(SO=200)에 가려짐
- AnimatedPanel은 항상 active 상태 → `_panel.Show()` 직접 호출, SetActive(true) 선호출 불필요
- BlockingOverlay(CanvasGroup): Show alpha=1/blocksRaycasts/interactable=true, Hide 0/false/false

### 알림 팝업(제목 + 본문 + 버튼 1개) — 프리팹 전제 조건 (2026-09-21)

- `ConfirmPopup.ShowAlert(title, message, buttonLabel, onClick)` 는 코드만으로 성립하지 않는다.
  `ApplyTitle()` 이 제목을 `SetActive(false)` 로 숨길 때 **그 자리가 레이아웃에서 사라져야** 하는데,
  그건 **Panel 에 VerticalLayoutGroup 이 있어야** 성립한다. 없으면 제목 자리가 빈 공간으로 남아
  제목 없는 기존 확인/취소 팝업의 생김새까지 바뀐다(= 회귀).
  같은 이유로 `SetCancelButtonVisible(false)` 는 ButtonRow 의 HorizontalLayoutGroup 에 의존한다.
- Panel 실측 **864 × 768px** (Canvas 1080×1920, Match=0(width), Panel 앵커 X 0.1~0.9 / Y 0.3~0.7).
  VLG 값: padding 69/69/92/61 · spacing 38 · MiddleCenter · ChildControl W/H ✓ · ForceExpandW ✓ ·
  **ForceExpandH ✗**. LayoutElement: TitleText pref 70 / flex 0, MessageText pref -1 / **flex 1**,
  ButtonRow pref 207 / flex 0.
- 🔴 **`ChildForceExpandHeight = true` 면 자식이 남는 높이를 균등 분할해 `preferredHeight` 가 전부
  무시된다.** 고정 높이 + "남는 높이는 한 칸만 가져간다(flexibleHeight=1)" 구조를 쓰려면 반드시 false.
  검산: 제목 없음 768−92−61−38−207 = **370px**(변경 전 369px 과 사실상 동일) /
  제목 있음 768−92−61−38×2−70−207 = **262px**.
- 셋업 스크립트: `Assets/Editor/Setup/SetupConfirmPopupAlertLayout.cs`
  (메뉴 `Hexiege/Setup/Setup ConfirmPopup Alert Layout`, 1회성).
  🔴 **1회성이지만 멱등이라 다시 실행해도 결과가 같다 — 이 프리팹의 레이아웃이 깨졌을 때
  되돌리는 수단이 이 스크립트다.** 첫 실행 로그는 「새로 만든 것 6건 / 갱신한 것 없음」이고,
  두 번째 실행부터는 갱신 쪽에 숫자가 잡히는 것이 정상이다.
  스크립트 패턴 자체는 [architecture.md](architecture.md) 「기존 프리팹 '에셋' 을 고치는 스크립트」가 단일 소스다.
  ⚠️ **이 스크립트를 삭제할지 `Tools/` 로 옮길지는 사용자 판단 대기다**
  (`WORKFLOW.md` [5-2] 는 `Setup/` 을 「실행 후 삭제 가능」으로 규정하는데 위 재사용 가치와 어긋난다).
  단일 소스는 `Assets/_Project/Docs/ROADMAP.md` 의 2026-09-21 신설 행.
- ⚠️ **제목·취소 버튼 숨김에 `CanvasGroup` 이 아니라 `SetActive` 를 쓴 것은 의도된 선택이며,
  공통 UI 규칙 5(CanvasGroup 숨김/표시 패턴)의 예외로 규칙 문서에 적을지는 사용자 판단 대기다.**
  근거(규칙 5 의 두 금지 사유가 여기서는 성립하지 않는다)는 `ConfirmPopup.ApplyTitle()` 의 XML 주석에 있다.
  🔴 **규칙 5 를 고치지 말 것** — 결정 전이다.

### 팝업 CloseButton 무반응 패턴
- CloseButton GO가 씬에 있어도 C#에 `[SerializeField] Button _closeButton` 필드 없으면 Inspector 연결 불가 → 무반응. 필드 추가 + OnCloseButtonClicked()→Hide()
- 컴포넌트 교체 시 `_panel` 등 슬롯 재연결 필수 (null이면 Show/Hide의 `if(_panel!=null)` 분기 전부 스킵)
- 팝업이 LoginRootView + LoginBootstrapper 양쪽 슬롯 연결 필요한 경우 있음 (Bootstrap이 Initialize, RootView가 Show)

---

## ToastUI

- 싱글턴 MonoBehaviour, IPointerClickHandler. `ToastUI.Show(ToastKey)` 정적 진입점. Queue 방식. DontDestroyOnLoad 독립 Canvas
- **씬 루트(부모 없음)에 배치** ([UI] Canvas 자식이면 씬 전환 파괴). SetActive(false) 금지(CanvasGroup.alpha=0)
- OnGameStarted/OnGameEnd 구독 자동 정리. ClearAll/FinishCurrent에서 SetActive 금지(루트 비활성 시 Update 정지)
- ToastKey는 Application/Events에 위치(2026-05-20 이동). ToastMessageConfig가 message/duration 보유
- **같은 키 중복 누적 방지(2026-09-08 수정, 실기 미검증)** — `Enqueue`가 ① 표시 중인 키와 같으면
  큐에 넣지 않고 `RestartCurrentDisplay()`(페이드 트윈 Kill → 알파 1 복구 → `_remainingDuration = _currentDuration`),
  ② `_queue.Contains(key)`면 무시, ③ 다른 키만 큐에 넣는다. 종전에는 무조건 `_queue.Enqueue`라
  버튼 5연타 = 같은 문구 5회 연속 노출이었다(빗금 타일뿐 아니라 골드 부족·인구 초과 등 **전 토스트 공통**).
  - `_currentKey`/`_currentDuration` 필드 신설. **duration 조회(`_config.TryGet`)는 `TryShowNext` 한 곳뿐** —
    되돌릴 때는 `_currentDuration` 재사용(조회 중복 금지).
  - 🔴 페이드아웃 중 재요청 시 **시간만 되돌리면 흐릿한 채로 남거나 트윈 완료로 사라진다.**
    `Kill()`은 기본 `complete:false`라 `OnComplete(FinishCurrent)`를 부르지 않는다 → Kill 후 알파 복구가 정답.
  - 중복 방지 덕분에 큐 길이 ≤ ToastKey 종류 수-1(현재 5) → `Queue<T>.Contains` O(n)로 충분(HashSet 불필요).
  - `ToastMessageConfig.asset` 실측: 6종 전부 `duration = 1`.
  - `GameSystemRules_UI.md` 「생산 패널 UI」 규칙 25(토스트 표시 방식)를 같은 커밋에서 함께 고쳤다 —
    「서로 다른 종류는 큐에 쌓고, 같은 종류는 누적하지 않는다(표시 중이면 시간만 리셋, 대기 중이면 무시)」.
    같은 문서 「생산 패널 UI」 규칙 28의 ToastKey 표도 3종 → 6종으로 채웠다(`ToastMessageConfig.asset` 실측 기준).
- **큐 로직 실행 검증 방법(재사용 가능)** — Presentation이라 Unity 컴파일은 불가하지만,
  `Enqueue`~`ClearAll` 본문을 `sed`로 **그대로 잘라** CanvasGroup/Tween/Time/Config 스텁과 함께 `mcs`로
  빌드하면 프레임 단위 시뮬레이션이 된다(`Text.text` setter를 표시 로그로 삼으면 표시 순서·횟수 측정 가능).
  DOTween 대역은 `Kill()`이 OnComplete를 부르지 않는 동작까지 재현해야 의미가 있다.

---

## 생산 패널(ProductionPopup) 실측 구조 — Game.unity (연구 패널 룩 기준)

- **루트 `ProductionPopup`**: full-stretch RectTransform + `Canvas(overrideSorting=true, sortingOrder=200)` + `GraphicRaycaster` + `AnimatedPanel(SlideFromBottom)` + `CanvasGroup` + `ProductionPanelUI`. (ProductionPanelUI/AnimatedPanel 모두 **루트에 같이** 부착)
- **프레임 `ProductionPanel`(루트의 유일한 자식)**: 앵커 (0,0)-(1,0.5) pivot(0.5,0) = **하단 절반·전체 너비**. Image sprite guid `c043043e4ea60bb4fb7a7fb7d7121c5e`(Simple).
- **헤더 `_headerText`**: Bold SDF(guid `96af9a121e352e245859ce1ae3a13b2b`).
- **닫기 `_cancelButton`**: **아이콘 스프라이트** guid `f5dbb98a85baad04eab27646d18ebdcc`(텍스트 라벨 없음), 우상단 앵커 (0.883,0.852)-(0.993,0.97) pivot(1,1).
- **유닛 버튼 Image**: 슬롯/버튼 프레임 sprite guid `704bb204bdd807f4abaa769a332ca9e4`(연구 버튼·행 배경 재사용 후보). 큐 슬롯 이미지는 sprite 없음.
- 폰트 GUID: Light SDF `58c71976882d99940aedcaa81b1248c5`, Bold SDF `96af9a121e352e245859ce1ae3a13b2b`. UIColorConfig `ce7db35dba9189c4e9d9c510f0a3bbce`(= Resources/Config/UIColorConfig.asset).

## 연구 패널 재구성 — 에디터 하베스트 패턴 (셋업 스크립트는 제거됨, 2026-07-27 이력)

- **접근(이력)**: 생산 패널 프리팹이 없다(씬 오브젝트). 그래서 셋업 에디터 스크립트가 에디터 실행 시 씬의 `ProductionPanelUI`를 리플렉션으로 읽어 배경 sprite/폰트/닫기아이콘/버튼 sprite/색상/Canvas SO/하단절반 앵커를 **라이브 하베스트**해 연구 패널에 적용했음. GUID 하드코딩 없음 → 블라인드/멱등 안전. 셋업 완료 후 스크립트는 역할 종료로 제거됨.
- `HarvestProductionStyle()` → `ProductionStyle` struct. `GetPrivateField(obj, "_headerText"/"_cancelButton"/"_unitButtons"/"_unitCostTexts")` 리플렉션(BaseType까지 탐색), `FirstUnityObject<T>(IList)`로 리스트 첫 요소.
- **레이어 버그 수정**: 구 연구 패널은 Canvas 오버라이드가 없어 BlockingOverlay(SO=100) 아래에 그려짐 → 루트에 `Canvas(overrideSorting, SO=200)` + `GraphicRaycaster` 추가.
- **멱등 이관**: 구조 재구성 시 루트 VLG/ContentSizeFitter 제거, 구 `HeaderRow`/`PlaceholderNote` 자식 삭제, `TrackContainer`는 재사용. 에셋 미발견 시 폴백 + 경고 로그(추정 배선 금지 규칙).
- ResearchPanelUI/ResearchTrackListView/ResearchTrackRowView 런타임 코드는 **미변경**(회귀 없음). 에디터 스크립트만 수정.
- ⚠️ 연구 패널은 여전히 CanvasGroup 즉시 alpha 방식(AnimatedPanel 슬라이드 애니메이션은 미적용 — 필요 시 후속).

## 건물 패널 UI 골격 — 씬 실측 (2026-08-10 확인 / 2026-08-21 복구)

> 2026-08-17 `675203ae` 로 `MEMORY.md` 에서 소실됐던 블록. 아래 중 **`Row0/Row1/Row2` 부모 구조는
> 다른 문서 어디에도 없는 유일본**이라 여기로 옮겨 보존한다.
> (`_allSlotButtons` 9개·index 5=철거 / `CostContainer` alpha=0 은 `WORK_HISTORY.md` 에도 있다.)

- `BuildingActionPanel` = 3×3 그리드 9슬롯 원본. `_allSlotButtons` 9개, **index 5 = 철거 버튼**
- 슬롯 자식 구조: `Slot1 { IconImage, CostContainer { GoldIcon, CostText } }`,
  **부모는 `Row0` / `Row1` / `Row2`(각각 HorizontalLayoutGroup)** ← 유일본
- 패널은 오버라이드 Canvas SortingOrder **200**(`GameSystemRules_CanvasSortingOrder.md`) — 복제하면 자동 충족
- 미사용 슬롯은 **`CanvasGroup.alpha=0`**, `SetActive(false)` 금지(GridLayout 정렬 붕괴)

### 자동모드 테두리 회전 머티리얼 — 공유 시 값이 달라지는 함정 (유일본)

- 에셋: `Assets/_Project/Materials/UI/mat_ui_rotatingborder.mat`
  (셰이더 `Shaders/UI/RotatingBorderUI.shader`)
- **에셋에 저장된 값은 `_Speed` 5 / `_Thickness` 0.05 뿐이다.**
- ⚠️ **`_Radius` · `_Inset` 은 `ProductionPanelUI` 가 런타임 인스턴싱으로만 덮어쓴다.**
  → 다른 패널이 이 머티리얼을 그대로 공유하면 두 값이 에셋 기본값으로 남아 **패널마다 테두리가 다르게 보인다.**
  새 패널에 붙일 때는 값을 런타임에 직접 세팅하거나 전용 머티리얼을 만든다.

---

## 씬 YAML 점검

- MonoBehaviour SerializeField 미연결: `{fileID: 0}`
- AnimatedPanel m_IsActive: MonoBehaviour(114) GUID 매칭 → m_GameObject fileID → 해당 GO body m_IsActive
- 폰트: Maplestory Light/Bold SDF (LiberationSans SDF 금지 — Rule 6)
- Canvas Scaler 1080×1920 ScaleWithScreenSize (Rule 1)

---

## Result screen — reflecting the opponent's leave, step 7 (2026-09-21)

Rule source: `GameSystemRules_UI.md` 「공통 UI 규칙」 **D-1**(timer wording) · **D-2**(rematch button off) ·
**D-3**(lobby button never off) · **D-4**(restart the countdown at **30s**). The verdict itself is steps 5·6 →
[network-infra.md](network-infra.md).

| File | What |
|---|---|
| `Presentation/UI/GameEndUI.cs` | one subscription to `GameEvents.OnNetworkOpponentLeft` → `OnOpponentLeft()`; `CountdownCoroutine` now takes `float totalSeconds`; leave-only entry `RestartCountdownForOpponentLeft()` |
| `Presentation/UI/NetworkStatusUI.cs` | suppresses the `DisconnectPanel` popup while the result screen is up |

- 🔴 **The leave wording lives in exactly one place**, `private const string OpponentLeftCountdownFormat`,
  and splits from the normal wording by a **single ternary inside `CountdownCoroutine`**. Verification grep is
  `grep -rn "초 뒤 로비로 이동합니다" Assets/_Project/Scripts/` → must be 1 hit (the const).
  ⚠️ **Do not quote that wording in comments** — a comment copy makes that grep unusable
  (this bit in the same session: two comments quoted it and had to be reworded).
- 🔴 **D-4's 30s is a `private const`, not a `[SerializeField]`** — same reason as step 6: a serialized field
  is written into `Game.unity` and the scene value then beats the code default (the `_autoReturnSeconds`
  30→60 case needed both places). ✅ Consequence: **step 7 needs no scene edit.**
- 🔴 **The 30s restart must not share an entry point with 규칙 M-3's restart** (that one restarts at the
  **full** length and is still unimplemented). Shape used: `CountdownCoroutine(float totalSeconds)` is the
  shared body, the two normal starts pass `_autoReturnSeconds`, and **only** `RestartCountdownForOpponentLeft()`
  passes 30s. M-3 will need its own entry method.
- **D-3 held by doing nothing**: `_backToLobbyButton` is never set to `false` anywhere in the repo; the only
  assignment left is `= true` in `RestoreRematchButton()`.
- **`RestoreRematchButton()` now refuses to re-enable the rematch button once `_opponentLeft` is set.**
  It is called by the rematch-declined *and* rematch-map-failed channels, either of which can arrive **after**
  the leave notification and would otherwise re-enable a button D-2 just turned off. The lobby-button line in
  the same method is untouched (D-3).
- `_opponentLeft` is reset in `Initialize()` (called per `LoadMap()`), so a rematch never starts on the
  previous match's wording.

### 🔴 Why the 「연결이 끊겼습니다」 popup is suppressed, and how (D-1)

- **The collision**: the leave verdict closes the connection (규칙 17), that shutdown reaches
  `NetworkGameManager.HandleClientDisconnected` (which `BackToLobby`/`DisconnectAsync` never unsubscribe, so it
  fires on *intentional* exits too), which raises `OnServerDisconnected`, which `NetworkStatusUI` turns into the
  `DisconnectPanel` popup — **on top of the result screen, covering the D-1 wording.**
- **Chosen fix**: `NetworkStatusUI` subscribes to `GameEvents.OnGameEnd` and sets `_resultScreenShown`;
  `OnServerDisconnected` returns early (with one dev-axis log line) while that flag is true.
- **Why keyed on 「the result screen is up」 rather than 「an opponent-left signal arrived」**: the flag is set
  when the result screen appears, i.e. **before** any disconnect can arrive, so the suppression cannot lose a
  race. Keying it on `OnNetworkOpponentLeft` would depend on that RPC/event landing before the transport
  callback — an ordering nobody can prove from code, and the normal-exit branch sends its RPC in the same frame
  as `Shutdown()`. 🔴 It also covers the branch where the *notification* never arrives at all.
- **Why in `NetworkStatusUI` and not in Infrastructure**: `OnServerDisconnected` is also the in-match path, and
  the owner of "which screen is showing" is Presentation. Suppressing at the publisher would kill both.
- ✅ **Untouched path**: a real in-match disconnect (before game end) still shows the popup — `_resultScreenShown`
  can only be true after `OnGameEnd`. `ForceWin` on a client whose link dropped also still shows it (that client
  never received `AnnounceWinnerClientRpc`, so no `OnGameEnd`).
- ⚠️ **Consequence to keep in mind**: after game end, *any* disconnect is now silent on that screen, including
  「my own internet died and the watchdog therefore refuses to judge」. The user is not trapped — D-3 keeps the
  lobby button on and D-4 auto-returns — but the only sign is the log line.
- ⚠️ **Unverified (no Unity here)**: compilation and every runtime behaviour, including whether the popup
  actually used to overlap (it is inferred from the code path, not observed).

> **[🔴 2026-09-22 correction — nothing above is deleted (`.claude/MEMORY.md` B-7). The answer to that last
> bullet came back **no**.]**
>
> 🔴 **`NetworkStatusUI` is not placed in any scene or prefab, so that popup never appeared at all.**
> Measured: the guid in `Presentation/UI/NetworkStatusUI.cs.meta` (`adc83ceead3e36246ba137d2ceede0a6`) has
> **0 hits** across every `*.unity` and `*.prefab` under `Assets`, and the string `NetworkStatusUI` — including
> `Start()`'s *"네트워크 상태 모니터링 시작"* — has **0 hits** in both 2026-09-22 logs (editor and device).
> The file header says *"씬 구조 (Inspector에서 수동 배치)"*, which is a **precondition, not a fact**.
> - **What this falsifies**: only the sentence *"…covering the D-1 wording"* above, read as **present tense**.
>   The collision is **not happening now** because the popup does not exist. The **ping (RTT) readout does not
>   run either**, and `DisconnectPanel` has no instance.
> - ⚠️ **The guard stays. This is not a reason to remove code.** Place `NetworkStatusUI` later and the overlap
>   becomes real, so the guard is valid prevention. **Nothing in the code was touched in the round that found
>   this** (docs only).
> - 🔴 **The *"✅ Untouched path"* bullet above is likewise true only about the code path** — an in-match
>   disconnect cannot show a popup that is in no scene. 2026-09-22 field test: the device (**Host**) was killed
>   mid-match and the remaining editor (**Client**) showed **no popup and no verdict**.
> - 🔴 **The lesson, not the fix, is the point**: before describing what a scene-placed component does, check
>   its placement by guid. `.claude/mistakes.md` **2026-09-22** is that entry.
> - **Single source for the fact and for the step-7 diagnosis correction**:
>   `Assets/_Project/Docs/TechnicalDesignDocument.md` 「결과 화면 이탈 판정·통보 구조」, the 2026-09-22 block.
>   The work item is the 「`NetworkStatusUI` 미배치」 row in `Assets/_Project/Docs/ROADMAP.md`;
>   🔴 **how to handle it (place it? is the popup needed at all?) is undecided** — do not read the note in that
>   row about the result screen carrying the notice instead as a decision.


## Result screen — step 8, rule D-6 (2026-09-22): the first real `ShowAlert` call site

Rule source: `GameSystemRules_UI.md` **D-6** (with **D-5** for the popup, **D-4** for the 30s countdown,
**8 · 9** for modal). Plan row: `_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md` §2, step **8**.

| File | What |
|---|---|
| `Presentation/UI/GameEndUI.cs` | 3 new `const` strings (title/message/button label) · non-serialized field `_rematchRequestPopup` · `HandleUnansweredRematchRequestOnOpponentLeft()` called at the end of the existing `OnOpponentLeft()` |
| `Presentation/UI/Common/RematchRequestPopup.cs` | state flag `_requestShowing` + `public bool TryCloseUnansweredRequest()` |

- 🔴 **The rematch request popup is `RematchRequestPopup`, and it IS placed** — unlike `NetworkStatusUI`.
  Measured: guid `d26ab2269c84ef641957a86f95840bd0` (from `RematchRequestPopup.cs.meta`) has **1 hit**, in
  `Assets/_Project/Scenes/Game.unity`, on GameObject `[UI]/RematchRequestPopup`, `m_IsActive: 1`, parent
  `[UI]` active, own Canvas `m_OverrideSorting: 1` / `m_SortingOrder: 250`, and **all 5 serialized fields
  are non-zero**. That is why `FindFirstObjectByType<RematchRequestPopup>()` is a safe resolution here
  (checking placement first is the `.claude/mistakes.md` 2026-09-22 lesson).
- 🔴 **D-6 is conditional and the code must be too.** The rule's first sentence limits it to *"the request
  popup is up and has not been answered yet"*. D-1 separately says an opponent-leave must **not** open a new
  popup (the timer text carries it). So `TryCloseUnansweredRequest()` returns **false when nothing was up**
  and `GameEndUI` then returns before `ShowAlert` — an unconditional alert would break D-1.
  Alpha cannot be used for that test (mid-fade it is neither 0 nor 1) → explicit bool flag, set in both
  `ShowRequest` overloads, cleared in `Hide()` (all close paths pass through it) and `ShowDeclined()`.
- **One subscription, extended — not a second one.** `GameEvents.OnNetworkOpponentLeft` still has exactly
  **1** subscription in `GameEndUI` (`grep -c` = 1). Two subscriptions to the same signal would leave the
  order of "close the popup" vs "open the alert" unprovable.
- **No new timer** (D-6 clause 3). The 30s is step 7's `RestartCountdownForOpponentLeft()`, called one line
  earlier in the same handler. The alert never self-closes; the button or D-4's expiry ends the screen.
- **The "로비로" button reuses `OnBackToLobbyClicked`** — the exact handler the lobby button installs — so
  countdown stop, `timeScale = 1`, loading indicator and the multiplayer NGO shutdown delegation cannot
  drift apart between the two buttons. `ConfirmPopup.OnConfirmClicked()` calls `Hide()` **before** the
  callback, so the overlay ref-count is released before the scene change.
- **Overlay ref-count across the handoff is safe**: `RematchRequestPopup.Hide()` takes it 1→0 and
  `ShowAlert` 0→1 in the same frame (no render in between), so rule order (close, then alert) costs nothing.
- ✅ **Prefab preconditions verified this time** (they are what D-5 depends on): `ConfirmPopup.prefab` has all
  **7** serialized fields non-zero including `_titleText`, Panel has a VerticalLayoutGroup with
  `m_ChildForceExpandHeight: 0` / spacing 38, ButtonRow HLG spacing 50. `UIManager._confirmPopup` is wired in
  `Login.unity`. `UIAnimator` sets `SetUpdate(true)` on every sequence, so the alert animates under
  `Time.timeScale = 0`.
- ✅ **No scene work**: the three D-6 strings are `const` (not `[SerializeField]`, which the scene would
  override) and the popup reference is resolved at runtime instead of wired.

### 🔴 Open defect found while doing this — NOT fixed, user decision pending

`ConfirmPopup` lives under `UIManager` (DontDestroyOnLoad; its only placement is `Login.unity`), while
`GameEndUI` and `RematchRequestPopup` die with the Game scene. So if the user **ignores** the alert and D-4's
30s expires, `ReturnToLobby()` changes scene **with the alert still open**: the Lobby comes up covered by the
alert plus the blocking overlay (ref-count stuck at 1), and pressing the leftover button then invokes
`OnBackToLobbyClicked` on a **destroyed** `GameEndUI` (`StopCoroutine` / `_panel.Hide()` on a destroyed
object). It self-recovers — `ConfirmPopup.Hide()` runs before the callback, so the overlay does clear — but it
leaves a stale popup and one exception.
🔴 **There is no `HideAlert`/`HideConfirm` on `IUIManager`/`UIManager`**, so `GameEndUI` cannot close it, and
adding one touches the shared API that Plan step 1 deliberately isolated. **Same trap will apply to step 10
(M-3 · M-4).** Options (not chosen): add `HideAlert()` to `IUIManager` + `UIManager` and call it from
`ReturnToLobby()`; or have `ConfirmPopup` close itself on scene unload (changes behaviour for every popup).

---

## 결과 화면 「재경기 준비 중」 상태 — 두 쪽이 서로 다른 신호로 들어간다 (2026-09-22)

`GameEndUI`. The status line is **one place only**: `_countdownText`. New state text const
`RematchPreparingStatusText = "재경기 준비 중..."`.

### 🔴 The rule that decided the whole design

> **「사람을 기다리면 타이머가 돈다. 시스템을 기다리면 타이머가 멈춘다.」**

*Requested* (waiting for a **person** to accept) → the 60s auto-return countdown **keeps running**; if the
opponent leaves the popup untouched the answer never comes, so the user must not be trapped.
*Accepted* (waiting for the **server** to build a map) → countdown **stops**, because that work terminates.

### 🔴 Entry paths differ by side — this is the actual fix

- **Accepter**: subscribes to `GameEvents.OnLocalRematchAccepted` — **its own button press**, not a server
  reply. Waiting for a reply would mean *nothing happens for a Client whose Host is already gone* (the
  ServerRpc evaporates) while a Host would proceed — i.e. the screen would differ by role.
  Note this subscription runs **alongside** the controller's on the same channel (see `network-infra.md`
  for why the controller's side is now try/catch-wrapped).
- **Requester**: subscribes to the new `GameEvents.OnNetworkRematchAccepted` (server notice). Without it the
  requester never learns acceptance happened and its own 60s expiry would drag it to the lobby mid-preparation.
- `EnterRematchPreparingState()` is **idempotent** (`_rematchPreparing`) because the accepter receives the
  broadcast notice too, and also returns early when `_opponentLeft` is already true.

### The limit clock — 🔴 it judges nothing

`RematchPreparingLimitCoroutine` only **restores the status line** (by restarting the countdown at full
length, rule M-3). It decides no win/loss, no leave, no disconnect. Do not bolt judgement onto it — this
screen already has two judging clocks (auto-return countdown, result-screen leave watch).

- Length is **computed, never literal**:
  `NetworkMapTransfer.TransferTimeoutSeconds * (NetworkMapTransfer.MaxResendCount + 1)`.
  One window = `TransferTimeoutSeconds`; window count = initial send 1 + `MaxResendCount` resends.
  🔴 **A single window is wrong** — one lost packet triggers a resend, the limit would expire on the
  *normal* path, the status line would revert and then the scene would suddenly reload. A false signal.
  🔴 There is **no digit `10` or `20` anywhere in `GameEndUI.cs`** (verified by grep) so a future change to
  rule 16's resend count follows automatically.
- **Stop sites — four, all wired** (a missed one leaves a dead clock):
  `OnRematchMapFailed()` · the `OnNetworkRematchStarting` subscription · `OnOpponentLeft()` · `OnDestroy()`.
  Plus a fifth defensive call in `Initialize()`.
- 🔴 **Self-stop hazard**: the coroutine nulls `_rematchPreparingCoroutine` **before** doing its work, or
  `StopRematchPreparingLimit()` reached from the restart path would `StopCoroutine(itself)` and cut the
  method off halfway.
- `RestartCountdownFromFullLength()` guards on `_opponentLeft` — otherwise a late map-failure notice would
  overwrite rule D-4's 30s leave countdown with 60s.

### 🔴 Rejected on purpose — do not "improve" these later

- **`ShowLoading` during preparation.** `LoadingScreen.Show()` sets `blocksRaycasts = true`, which would make
  the 「로비로」 button unpressable — a direct rule D-3 violation. Only the status line changes.
  (The pre-existing loading on `OnNetworkRematchStarting` stays untouched; its literal string happens to read
  the same as the new const, and they were **not** merged because that line was out of scope.)
- Blocking the buttons outright (adds another clock) and a separate failure popup (rule D-1 forbids it).

### `RestoreRematchButton()` — label restored outside the guard (behaviour change)

The label assignment used to sit **inside** `if (_restartButton != null && !_opponentLeft)`, so on a leave
judgement the text stayed frozen at 「요청 중...」. Now: label → 「다시하기」 **unconditionally**;
`interactable = !_opponentLeft` keeps the D-2 guard. `OnOpponentLeft()` calls this method (after setting
`_opponentLeft = true`, order matters) instead of poking `interactable` directly.
✅ `_backToLobbyButton.interactable = false` remains **0 occurrences repo-wide** (rule D-3).

⚠️ **Unverified**: compilation and runtime. Static checks only — brace balance 36/36 after stripping
comments/strings; no scene or prefab was touched (every new value is a `const` or a referenced constant).

### 재경기 실패 — 3경로를 상태 줄 문구 하나로 묶었다 (2026-09-22, 사용자 확정)

🔴 **재경기가 안 되는 길은 셋인데 사용자는 구분할 수 없고 구분할 필요도 없다.** 전부 「재경기가 안 됐다」다.
So all three converge on **one** entry point, `EnterRematchFailedState()` — **never copy the handling into the
three sites.** Call sites: 2 (the third path shares a handler).

| path | how it arrives | handler |
|---|---|---|
| ① couldn't send the accept at all | controller publishes `OnNetworkRematchMapFailed` **locally** from its catch | `OnRematchMapFailed()` |
| ② server says map prep failed | same channel, via ClientRpc (original use) | `OnRematchMapFailed()` |
| ③ prep limit expired with no notice | — | `RematchPreparingLimitCoroutine()` |

- Text: `RematchFailedCountdownFormat = "재경기를 시작할 수 없습니다. {0}초 후 로비로 돌아갑니다."` —
  a **format string in one const**, same shape as `OpponentLeftCountdownFormat`.
- `EnterRematchFailedState()` does four things, **in this order**: stop the prep limit → set `_rematchFailed`
  → `RestoreRematchButton()` → `RestartCountdownFromFullLength()` (rule M-3, full length).
  🔴 **The flag must be set *before* the countdown restarts** — the coroutine reads it every second, so
  setting it after makes the first second show the normal text and then flip (visible flicker).
- 🔴 **`CountdownCoroutine`'s branch is now 3-way and the priority is 이탈 > 실패 > 평시.**
  The two can hold at once: the prep limit expires first, then the leave judgement (30s) lands. The screen
  must keep the **later, more fundamental** fact, because 「상대가 떠났다」 is the *cause* of
  「재경기가 안 됐다」 — showing the cause beats showing the effect, and the reverse order would destroy the
  only chance to tell the user the opponent left.
- **`_rematchFailed` is cleared in three places** (a stale flag makes the screen keep claiming failure while a
  retry is already running): the `SetupRematchButton` onClick, `EnterRematchPreparingState()`, `Initialize()`.
- 🔴 **No popup.** The user chose "status line, and amend rule M-3" over M-3's 2026-09-16 決 of
  「알림 팝업으로 알린다」. A popup covers/blocks the 「로비로」 button → rule D-3 violation. **`ShowAlert` was
  not added** (the only `ShowAlert` call in the file remains step 8's D-6 opponent-left alert).
  The reason is written next to the const so nobody puts a popup back.
- Buttons unchanged in behaviour: `RestoreRematchButton()` restores the label and leaves `interactable`
  false when `_opponentLeft`; `_backToLobbyButton.interactable = false` stays **0 occurrences**.

### 재경기 실패 문구를 사유에 따라 가른다 — 단계 9 (2026-09-24, 규칙 18)

앞 절(2026-09-22)이 **실패 3경로를 문구 하나로 묶었다.** 여기서는 그 **「실패」 분기 안쪽이 둘로 갈린다.**
🔴 **우선순위는 그대로 「이탈 > 실패 > 평시」** — 바뀐 것은 **실패 안쪽**뿐이다.

| 사유 | 상수 | 문구 |
|---|---|---|
| `OpponentDisconnected` | `RematchFailedByOpponentLeftCountdownFormat`(신설) | 「상대방이 나가서 재경기를 시작할 수 없습니다. {0}초 후 …」 |
| 그 밖(모르는 경우 포함) | `RematchFailedCountdownFormat`(그대로) | 「재경기를 시작할 수 없습니다. {0}초 후 …」 |

- 🔴 **문구 분기는 여전히 한 자리다** — `CountdownCoroutine` 안의 **삼항 3분기 그대로**이고, 실패 쪽 항만
  `string.Format(SelectRematchFailedFormat(), seconds)` 로 바뀌었다. 🔴 **`SelectRematchFailedFormat()` 은
  형식 문자열을 고르기만 하고 화면에 쓰지 않는다** — 분기를 다른 메서드로 흩지 않기 위한 형태다.
  (본문에 `if` 를 늘어놓으면 「문구를 쓰는 자리가 한 곳」이라는 성질이 깨진다.)
- **사유는 `EnterRematchFailedState(RematchMapFailureCause cause)` 가 받아 `_rematchFailureCause` 에 보관한다.**
  🔴 **사유를 깃발(`_rematchFailed`)보다 먼저 대입한다** — 코루틴이 매 초 둘을 함께 읽으므로 순서가 뒤집히면
  첫 1초에 잘못된 문구가 보인다(깃발이 카운트다운 재시작보다 앞이어야 하는 것과 같은 이유의 순서).
- **실패 3경로가 모두 사유를 넘긴다**: ① 수락 전송 실패 → `Unknown`(컨트롤러가 로컬 발행) ·
  ② 서버 통보 → 서버가 판정한 값 · ③ 한도 만료 → `Unknown`.
  🔴 **①③ 이 `Unknown` 인 근거는 「모른다」이지 「연결 끊김이 아니다」가 아니다** — 상대가 나갔을 수도,
  내 회선일 수도 있다. 단정하면 거짓이 될 수 있어 중립 문구를 쓴다(`CLAUDE.md` 규칙 10). 코드 주석에 남겼다.
- **`_rematchFailureCause` 를 되돌리는 자리는 `_rematchFailed` 와 완전히 같은 3곳**
  (`SetupRematchButton` onClick · `EnterRematchPreparingState` · `Initialize`). 같은 줄 바로 아래에 붙여
  한쪽만 빠뜨릴 수 없게 했다.
- **구독은 늘지 않았다** — `GameEvents.OnNetworkRematchMapFailed` 의 타입만 payload 로 바뀌었고
  구독은 여전히 `GameEndUI` **1곳**, 발행은 **2곳**(자세한 내용은 [network-infra.md](network-infra.md) 같은 날짜 절).
- 🔴 **문구를 주석에 베껴 적지 않았다** — 단계 7 에서 겪은 그대로다(주석 사본 하나가 「문구가 한 곳에만 있는가」
  grep 을 무용하게 만든다). 주석에서는 문구 대신 **상수 이름**을 가리키고, 뜻을 말해야 할 때는 규칙 18 의 표현
  (「상대가 나갔다」)을 쓴다 — **리터럴의 부분 문자열이 되지 않는 낱말이어야 한다.**
  검증 grep: `grep -rn '상대방이 나가서' Assets/_Project/Scripts Assets/Editor` → **1건(상수뿐)**.
- ✅ **`ShowAlert` 실호출은 늘지 않았다**(규칙 D-6 이탈 알림 1건 그대로 — 규칙 M-3 은 상태 줄이다) ·
  `_backToLobbyButton.interactable = false` **0건 유지**(규칙 D-3) · **씬·프리팹 0건**(새 값은 전부 `const`).
- ⚠️ **미검증**: 컴파일(Unity 없음)과 런타임 전부. 확인한 것은 중괄호 균형과 `mcs` 파싱(구문 오류 0)뿐이다.
