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

## 씬 YAML 점검

- MonoBehaviour SerializeField 미연결: `{fileID: 0}`
- AnimatedPanel m_IsActive: MonoBehaviour(114) GUID 매칭 → m_GameObject fileID → 해당 GO body m_IsActive
- 폰트: Maplestory Light/Bold SDF (LiberationSans SDF 금지 — Rule 6)
- Canvas Scaler 1080×1920 ScaleWithScreenSize (Rule 1)
