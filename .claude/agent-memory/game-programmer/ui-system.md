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
