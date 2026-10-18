# Game System Rules — UI

- **목적·답하는 질문:** 모든 화면에 공통 적용되는 UI 동작과 팝업·로딩 책임을 판단한다.
- **포함:** 공통 UI·팝업·로딩 원본 및 분리 문서로 향하는 기존 제목·번호 안내
- **제외·담당 문서:** 경기 흐름·건물 패널·설정의 동작은 아래 담당표, 시각 스타일은 [UIGuidelines](../UIGuidelines.md), 정렬 대역은 [CanvasSortingOrder](GameSystemRules_CanvasSortingOrder.md)
- **권위원본·주요 참조:** 공통 규칙 원본이며 분리 영역은 호환 안내
- **갱신 조건·방법:** 공통 동작 승인 변경 때 원본만 갱신하고 안내 ID·제목은 유지한다. 개별 화면의 완료·검증 보고는 상태·이력 문서에 둔다.

| 담당 범위 | 규칙 원본 |
|---|---|
| 경기 준비 실패·결과·재경기 화면 | [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md) |
| 생산·MistShrine·건물 배치 패널 | [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md) |
| 인게임·로비 설정·프로필 | [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md) |

> ⚠️ 이 문서는 **각 섹션이 규칙 번호를 1부터 다시 시작**한다. 따라서 "규칙 6"이라고만 적으면 어느 규칙인지 특정할 수 없다.
> 이 문서 안에서든 **다른 문서에서 참조할 때든** 반드시 아래 목차의 섹션 이름을 함께 적는다
> (예: "공통 UI 규칙 6", "인게임 설정 메뉴 규칙 6").
> 규칙 번호는 코드 주석과 과거 작업 문서가 참조하고 있으므로 **번호 자체를 재배열하지 않는다** — 어긋난 것은 참조하는 쪽을 고친다.

> Canvas SortingOrder 구조 및 씬별 Canvas 목록 → [`GameSystemRules_CanvasSortingOrder.md`](GameSystemRules_CanvasSortingOrder.md) 참조

---

## 목차

- [공통 UI 규칙](#공통-ui-규칙)
- [생산 패널 UI](#생산-패널-ui)
- [MistShrine 패널 UI](#mistshrine-패널-ui)
- [건물 배치 패널 UI](#건물-배치-패널-ui)
- [인게임 설정 메뉴](#인게임-설정-메뉴)
- [로비 설정/프로필 UI](#로비-설정프로필-ui)

---

## 공통 UI 규칙

모든 UI 시스템에 예외 없이 적용되는 공통 규칙이다.
새로운 UI를 제작하거나 기존 UI를 수정할 때 이 섹션을 기준으로 삼는다.

멀티플레이 UI는 [네트워크 최상위 원칙](GameSystemRules_Network.md)을 따른다. 같은 상황의 화면이 Host/Client 역할로 달라지면 결함이며 경기 흐름 UI에도 동일하게 적용한다. 이관·덧붙임 지침의 이전 원문은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-ui-01)다.

---

### 반응형 UI

**규칙 1. Canvas Scaler 설정**
모든 씬의 Canvas는 아래 값으로 통일한다.

| 항목 | 값 |
|------|-----|
| Scale Mode | Scale With Screen Size |
| Reference Resolution | 1080 × 1920 |
| Match Width Or Height | 0 (가로 너비 기준) |

Match Width Or Height=0은 가로 너비를 기준으로 스케일링한다.
세로가 긴 기기(21:9 등)에서는 캔버스 논리 높이가 기준보다 자연스럽게 늘어나는 방식으로 대응한다.

**규칙 2. 앵커 기반 배치 원칙**
모든 UI 요소는 고정 픽셀 크기 대신 앵커 비율 기반으로 배치한다.
sizeDelta, offsetMin, offsetMax 등 고정 픽셀값 사용 금지.

앵커(Anchor)란 부모 오브젝트를 기준으로 자식 오브젝트의 위치와 크기를 비율로 지정하는 방식이다.
화면 크기가 달라져도 비율이 유지되므로 어떤 해상도에서도 레이아웃이 일관되게 표시된다.

Layout Group 내부 반응형 패턴:
GridLayoutGroup의 CellSize는 고정 픽셀값이므로 사용하지 않는다.
대신 VerticalLayoutGroup + HorizontalLayoutGroup 중첩 구조를 사용하고,
Control Child Size + Child Force Expand를 활성화하면 CellSize 없이
가용 공간을 자동으로 균등 분배한다.

**규칙 3. Filled/Simple 이미지 자식 앵커 규칙**
Image.Type.Filled 또는 Simple 타입 이미지 안에 자식 이미지가 있는 경우,
자식 이미지도 부모와 같은 비율로 앵커를 설정해야 한다.
고정 offsetMin/offsetMax를 사용하면 해상도마다 자식 이미지의 크기와 위치가 달라진다.

예: 게이지 바(부모, Filled) 안에 fill 이미지(자식)가 있을 때,
fill 이미지의 앵커를 0.14~0.86처럼 비율로 설정해야 모든 해상도에서 동일하게 표시된다.

---

### Safe Area

**규칙 4. SafeArea 컨테이너 구조**
노치, 펀치홀, 하단 홈바가 있는 기기에서 UI가 가려지지 않도록 모든 씬에 SafeAreaContainer를 배치한다.
SafeAreaContainer에는 SafeAreaFitter 컴포넌트를 부착하여 Safe Area 범위에 맞게 자동으로 조정한다.

```
[UI] Canvas
  ├─ Background               ← 전체화면 배경 등 Safe Area 적용이 불필요한 요소
  └─ SafeAreaContainer        ← SafeAreaFitter 컴포넌트 부착
       └─ (모든 실제 UI 요소)  ← 팝업, HUD, 버튼 등 전부 이 안에 배치
```

- 실제 UI 요소(팝업, HUD 등)는 전부 SafeAreaContainer 안에 배치한다.
- 전체화면을 채워야 하는 배경 요소는 SafeAreaContainer 밖(Canvas 직속)에 배치한다.
- 독립 Canvas를 가진 UI(ToastUI 등)는 해당 Canvas에도 SafeAreaFitter를 적용한다.
- **반투명 배경 오버레이(BlockingOverlay)도 전체화면 배경과 동일하게 SafeAreaContainer 밖(UIManager Canvas 직속)에 두어야 한다.**
  팝업 컴포넌트가 직접 소유하지 않고, `UIManager.ShowBlockingOverlay()` / `HideBlockingOverlay()`를 통해 제어한다.
  (각 팝업이 SafeAreaContainer 안에 개별 오버레이를 소유하면 노치/홈바 영역이 가려지지 않는 구조적 문제가 발생하므로, UIManager가 단일 소유한다.)

**전체화면 배경 오브젝트 구현 방법**

배경 오브젝트를 Canvas 직속에 올바르게 배치하기 위해 아래 4가지를 반드시 지킨다.

1. **RectTransform 설정**: anchorMin=(0,0), anchorMax=(1,1), offsetMin=offsetMax=(0,0) → Canvas 전체를 빈틈없이 채움
2. **raycastTarget=false**: Image 컴포넌트의 Raycast Target을 반드시 꺼야 한다. 켜두면 배경이 터치 이벤트를 모두 가로채 하위 UI 버튼이 눌리지 않는다.
3. **Hierarchy 순서**: 배경 오브젝트를 SafeAreaContainer보다 위(앞)에 배치한다. Unity UI는 Hierarchy 위쪽 오브젝트를 먼저(뒤에) 그리므로, 배경이 SafeAreaContainer 아래에 있으면 모든 UI를 가린다.
4. **기존 Image 처리**: SafeAreaContainer 안에 있던 오브젝트에 배경 역할의 Image가 붙어 있었다면, 별도 배경 오브젝트를 생성한 뒤 기존 Image는 삭제 대신 `enabled=false`로 비활성화한다. 실기 테스트 통과 후 최종 삭제한다.

---

### UI 숨김/표시

**규칙 5. CanvasGroup 숨김/표시 패턴**
UI 요소를 숨길 때 SetActive(false) 대신 CanvasGroup을 사용한다.

| 상태 | alpha | blocksRaycasts | interactable |
|------|-------|----------------|--------------|
| 숨김 | 0 | false | false |
| 표시 | 1 | true | true |

SetActive(false)를 사용하면 두 가지 문제가 발생한다.
- Layout Group(버튼들이 나란히 정렬되는 구조) 안에서 해당 요소가 차지하던 공간이 사라져 나머지 요소들이 이동한다.
- 오브젝트 내부 로직(Update 등)이 함께 멈춘다. DontDestroyOnLoad 오브젝트(ToastUI 등)에서 이 방식을 쓰면 이후 동작이 전혀 일어나지 않는다.

**DOTween / AnimatedPanel 사용 시 추가 주의사항**:
- DOTween 애니메이션이 진행 중인 오브젝트를 SetActive(false)로 비활성화하면 Tween 시퀀스가 강제 중단되고, 이후 Show() 호출 시 초기화 타이밍이 꼬일 수 있다.
- `AnimatedPanel` 컴포넌트가 붙은 오브젝트는 **항상 active 상태로 유지**한다. `EnsureInitialized()`에서 CanvasGroup을 `alpha=0 / blocksRaycasts=false / interactable=false`로 초기 설정하여 시각적으로 숨긴다.
- 배경 오버레이(`_backgroundOverlay`)도 GameObject는 항상 active 상태 유지. Show() 시 `alpha=1 / blocksRaycasts=true / interactable=true`, Hide() 시 `alpha=0 / false / false`로 즉시 전환한다.

**반투명 배경 오버레이(BlockingOverlay) 단일 소유 패턴**:
- 반투명 배경 오버레이는 **UIManager가 단일 소유**한다. 개별 팝업이 자체 오버레이를 들고 있지 않으며, `UIManager.ShowBlockingOverlay(onTap)` / `UIManager.HideBlockingOverlay()`로만 제어한다.
- 두 가지 모드로 구분한다.
  - **Modal 모드** — `ShowBlockingOverlay()` (콜백 없음): 뒤쪽 입력만 차단한다. 오버레이를 터치해도 닫히지 않는다. (ConfirmPopup, AnonymousWarningPopup, RematchRequestPopup)
  - **Popup 모드** — `ShowBlockingOverlay(() => Close())` (콜백 있음): 오버레이를 터치하면 등록된 콜백(팝업 닫기)이 실행된다. (InGameSettingsUI, BuildingPlacementUI, 생산/건물 패널)
- 팝업이 중첩될 수 있으므로 UIManager는 **참조 카운터**로 표시 횟수를 누적 관리한다. `HideBlockingOverlay()`가 호출되어 카운터가 0이 될 때에만 실제로 숨겨진다.
- 호출은 항상 null-safe 패턴 `UIManager.Instance?.ShowBlockingOverlay(...)`를 사용한다 (씬 직접 진입 시 Instance가 null일 수 있음).
- **팝업 Hide와 오버레이 Hide는 항상 짝을 이룬다.** 조준 모드 진입 등을 위해 팝업만 숨기는 경로에서도 오버레이를 그대로 두지 말고 `HideBlockingOverlay()`로 함께 내린다. 반대로 조준 완료/취소 처리가 `Close()`를 거치지 않고 끝나는 경로는, `Close()`가 대신 반납해 주던 참조 카운터를 해당 경로에서 직접 `HideBlockingOverlay()`를 호출해 반납해야 한다. (동일 결함이 스킬 패널·랠리포인트 두 곳에서 반복 발생했던 사례 — `_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`)

---

### 폰트

**규칙 6. 기본 폰트**
TextMeshPro 텍스트 컴포넌트에 사용하는 폰트는 아래 두 가지다.

| 폰트 | 경로 | 용도 |
|------|------|------|
| Maplestory Light SDF | `Assets/_Project/Fonts/Maplestory Light SDF.asset` | 기본 폰트 (일반 본문, 라벨, 수치 등) |
| Maplestory Bold SDF | `Assets/_Project/Fonts/Maplestory Bold SDF.asset` | 강조 폰트 (헤더, 버튼 등 굵은 표현 필요 시) |

기본값은 Maplestory Light SDF이며, 강조가 필요한 경우 Bold SDF를 사용한다.
에디터 스크립트로 UI를 생성할 때도 폰트 에셋을 명시적으로 설정해야 한다.
이 두 폰트 외의 폰트는 사용하지 않는다.

---

### 골드 부족 UI

**규칙 7. 비용 텍스트 색상**
보유 골드가 해당 항목(유닛, 건물)의 비용보다 적으면 빨간색, 충분하면 흰색으로 표시한다.
골드가 변경될 때마다 자동으로 재평가한다.
팝업이 닫히면 흰색으로 초기화한다.

골드와 비용을 비교해서 색상을 결정하는 로직은 각 UI에서 동일하게 적용되어야 하며,
이 규칙을 따르는 개별 섹션에서는 해당 규칙을 별도로 기술하지 않고 이 규칙을 참조한다.

---

### 팝업

**규칙 8. 팝업 타입 구분**
모든 팝업은 아래 두 가지 타입 중 하나로 반드시 분류한다.
새로운 팝업을 제작할 때 어떤 타입인지 명확히 결정하고 구현해야 한다.

| 타입 | 설명 | 배경 탭 닫기 |
|------|------|-------------|
| 팝업 (Popup) | 정보 확인 또는 조작 패널 | 가능 |
| 모달 (Modal) | 사용자의 명시적 응답이 필요한 팝업 | 불가 |

모달의 기준은 **화면을 진행하려면 사용자가 팝업 안의 버튼으로 응답해야 하는가**다. 확인 버튼 하나만 있는 알림도 모달이며 응답 버튼 수가 두 개일 필요는 없다. 기존 모달의 타입과 배경 탭 금지는 유지한다.

현재 예시:
- 팝업: 생산 패널, 건물 배치 패널, 인게임 설정 메뉴
- 모달: ConfirmPopup(포기 확인 등), 맵 준비 실패 UI(멀티 최초 경기·싱글 최초 경기), 재경기 요청, **알림 팝업**. 알림은 [경기 흐름 UI 규칙 D-5](GameSystemRules_MatchFlowUI.md#경기-종료-후-상대-이탈-ui)가 담당하며 멀티 최초 경기 맵 준비 실패(M-2)와 재경기 요청자 이탈(D-6)에 사용한다.

**규칙 9. 팝업 닫기 — 배경 탭**
팝업(Popup) 타입은 배경을 탭하면 닫힌다.
모달(Modal) 타입은 배경 탭이 비활성화되며, **팝업 안의 명시적 응답 버튼으로만** 닫힌다. 확인/취소 두 버튼 또는 확인 버튼 하나인 구성 모두 이 조건을 따른다.

모달 정의·버튼 해석·이전 예시의 개정 원문은 [당시 이력](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-ui-01)에 보존한다.

**규칙 10. 팝업 중첩**
팝업이 중첩되어 열릴 수 있으며 다음 규칙을 따른다.
- 중첩 팝업이 열리면 하위 팝업은 즉시 입력이 차단된다.
- 닫기 순서는 LIFO (마지막에 열린 팝업부터 순서대로 닫힌다).
- 배경(Background)은 하나의 공유 배경을 사용한다.

**규칙 11. 건물 팝업 — 대상 건물 파괴 시 자동 닫힘 (2026-08-08 신설)**
건물을 대상으로 열리는 모든 건물 패널(생산/건물액션/스킬/연구 — 공통 베이스 `BuildingPanelBase`)은, **현재 표시(또는 스킬 조준) 중인 그 건물이 파괴되면 즉시 닫혀야 한다.** 파괴된 건물의 패널·조준 UI가 화면에 남는 유령 상태를 허용하지 않는다.

- 판정은 팝업의 가시성(`IsOpen`)이 아니라 **현재 대상 건물의 Id 일치**로 한다. 스킬 조준 모드에서는 패널 팝업이 숨겨져(`IsOpen=false`) 가시성만으로는 조준 중 케이스를 놓치기 때문이다.
- 닫힘은 각 패널의 정리 훅(`OnBeforeClose`)을 반드시 경유해, 스킬 조준 취소·랠리 마커 숨김 등 패널별 정리가 함께 실행되도록 한다.
- 파괴 인지는 서버 권위 사망 이벤트(`GameEvents.OnBuildingDied`)를 기준으로 하며, 멀티플레이 클라이언트에서도 동일 이벤트가 재발행되어 싱글/호스트/순수 클라 모두 동일하게 동작한다.
- 게임 상태 판정(발동·철거 등)은 서버 재검증이 권위를 가지며, 본 규칙은 **UI 원복만** 담당한다.

---

### 로딩 인디케이터 (LoadingIndicator)

**규칙 L-1. LoadingIndicator는 UIManager.ShowLoading()으로만 제어**
- `UIManager.Instance?.ShowLoading(true, "메시지")` / `ShowLoading(false)`
- 직접 LoadingIndicator GameObject를 활성/비활성 금지.

**규칙 L-2. 씬 전환이나 비동기 작업 시작 시 반드시 ShowLoading(true) 호출**
- 해당하는 상황: 씬 전환(LoginScene/LobbyScene/GameScene), 로그아웃, 로비 복귀, 재경기.
- 게임 포기(싱글/멀티 모두)는 씬 전환 없이 같은 씬 안에서 GameEndUI만 표시하므로 해당 없음.

**규칙 L-3. ShowLoading(false) 책임은 목적지 씬 Bootstrapper**
- 씬을 시작하는 호출부는 ShowLoading(true)만 담당한다.
- ShowLoading(false)는 각 씬의 초기화 완료 시점에 호출한다.
  - Login 씬: LoginBootstrapper(ShowLoginSelect / 자동 로그인 분기).
  - Lobby 씬: LobbyRootView.Start() 완료 시점.
  - Game 씬: GameBootstrapper.LoadMap() 완료 시점.
- 어디서 켰든 목적지 씬이 준비되면 자동으로 꺼지므로 ShowLoading(false) 누락 버그를 방지한다.
- 예외 경로(비동기 작업 실패로 씬 전환이 무산된 경우)에서는 켠 호출부가 직접 ShowLoading(false)로 끈다.

**규칙 L-4. null-safe 패턴 필수**
- `UIManager.Instance?.ShowLoading(...)` — Lobby/Game 씬 단독 실행 시 Instance가 null일 수 있으므로 null-safe를 보장한다.

> Infrastructure(NetworkBehaviour)에서 발생하는 씬 전환(재경기 등)은 UIManager(Presentation)를 직접 참조하지 않고
> GameEvents(Application)를 경유해 Presentation(GameEndUI)이 ShowLoading을 호출하도록 한다(레이어 방향 보호).

---

### 무작위 맵 준비 실패 UI

**규칙 M-1. 내부 정보 비공개**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 M-2. 멀티플레이 최초 경기 실패**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 M-3. 멀티플레이 재경기 맵 준비 실패**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 M-4. 싱글플레이 실패**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 경기 종료 후 상대 이탈 UI

**규칙 D-1. 상대 이탈 시 결과 화면의 타이머 텍스트를 교체한다**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 D-2. 상대 이탈 시 재경기 버튼을 비활성화한다**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 D-3. 로비 복귀 버튼은 항상 활성 상태를 유지한다**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 D-4. 자동 로비 복귀 카운트다운 — 기본 60초, 이탈 판정 시 30초로 재시작**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 D-5. 알림 팝업 — 타이틀 + 본문 + 버튼 1개, 배경 탭으로 닫히지 않는다**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 D-6. 재경기 요청에 응답하기 전에 요청자가 이탈한 경우**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 D-7. 재경기 수락이 접수되면 상태 줄을 「재경기 준비 중...」으로 바꾸고 자동 복귀 타이머를 멈춘다**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

D-7 하위 판단·수락 접수·준비 한도·시계·기각안은 [경기 흐름 UI의 D-7](GameSystemRules_MatchFlowUI.md#판단-기준--이-한-문장이-위-표를-결정한다)에 통합되어 있다. 이전 빈 하위 제목 목록은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-ui-02)다.

**규칙 D-8. 「요청 중...」 문구는 버튼에 두고, 상대 이탈 뒤에는 문구만 되돌리고 버튼은 비활성으로 유지한다**

본문 원본: [GameSystemRules_MatchFlowUI.md](GameSystemRules_MatchFlowUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

## 생산 패널 UI

### 팝업 열기/닫기

**규칙 1. 팝업 열기**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 2. 팝업 닫기**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 유닛 버튼 입력

**규칙 3. 탭**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 4. 롱프레스**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 5. 자동 생산 중인 유닛 탭**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 큐 구조

**규칙 6. 슬롯 구성**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 7. 큐 상한 계산**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 8. 슬롯 클릭 취소**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 골드 차감 시점

**규칙 9. 수동 항목**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 10. 자동 항목**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 생산 등록 검증 순서

**규칙 11. 검증 우선순위**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 생산 실패 피드백

**규칙 12. 실패 피드백 방식**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 정보 바 (골드/인구/비용 텍스트)

**규칙 13. 골드 텍스트**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 14. 유닛 비용 텍스트 색상**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 15. 인구 텍스트**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### HUD (골드/인구 상시 표시)

**규칙 16. 인구 가득 상태 표시**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 17. 인구 텍스트 형식**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 자동 생산 규칙

**규칙 18. 수동 등록 시 자동 모드 해제**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 19. 자동 생산 해제/취소 시**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 20. 자동 생산 중복 방지**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 21. 자동 생산 중 자원 부족 시**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 22. 생산 완료 후 자동 순환**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 슬롯 직접 취소 규칙

**규칙 23. 취소 시 환불**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 스폰 대기

**규칙 24. 스폰 불가 시 대기**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 토스트 메시지

**규칙 25. 토스트 표시 방식**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 26. 토스트 터치**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 27. 토스트 자동 정리**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 28. 토스트 종류**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

## MistShrine 패널 UI

### 패널 구조

**규칙 1. `BuildingPanelBase`를 상속한 전용 패널을 신설한다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 2. `BuildingSkillPanelUI`는 재사용하지 않는다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 3. 패널 구성 요소**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 4. 닫기 규칙은 공통 팝업 규칙을 따른다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 조작 (생산 패널과 동일하게 통일)

**규칙 5. 탭 / 롱프레스 조작**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 6. 자동 모드 기본값은 OFF**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 쿨다운 표시

**규칙 7. 쿨다운 오버레이는 `SkillCooldownOverlay`를 재사용한다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 범위 표시 UI

**규칙 8. 아군 MistShrine 패널이 열려 있는 동안에만 범위를 표시한다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 회복 텍스트

**규칙 9. 회복 텍스트는 회복이 실제로 발생한 대상만 · 표시 주기를 힐 틱과 분리한다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 패널 레이아웃 (2026-08-10 확정)

**규칙 10. 패널 레이아웃은 3×3 그리드 9슬롯을 유지한다 (A안 확정)**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 11. 미사용 슬롯은 `CanvasGroup.alpha=0`으로 숨긴다 — `SetActive(false)` 금지**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 범위 표시 원의 시각 사양 (2026-08-10 확정)

**규칙 12. 범위 표시 원은 반투명 채움 + 외곽선을 함께 사용한다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 물안개 지속 중 사용 버튼 상태 (2026-08-10 확정)

**규칙 13. 물안개 지속 중에도 사용 버튼에는 쿨다운 오버레이만 표시한다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 자동 모드 시각 표시 (2026-08-10 확정)

**규칙 14. 자동 모드 표시는 테두리 회전 효과만 사용하고, 오버레이 오브젝트는 하나만 둔다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 미확정 항목

**규칙 15. 사용(시전) 버튼 아이콘은 미제작이다**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

## 건물 배치 패널 UI

### 비용 텍스트

**규칙 1. 건물 비용 텍스트 표기 형식**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 건물 비용 텍스트 색상

**규칙 2. 색상 표시 기준**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 3. 색상 재평가 시점**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 건물 배치 실패 피드백

**규칙 4. 골드 부족 시 피드백**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 무작위 맵 타일 선택과 건설 패널

**규칙 5. 판정 우선순위**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 6. 막힌 타일 또는 빈 공간 클릭**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 7. 자기 팀 소유 건설 불가 타일 클릭**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 8. 중립·적 소유 건설 불가 타일 클릭**

본문 원본: [GameSystemRules_BuildingPanelsUI.md](GameSystemRules_BuildingPanelsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

## 인게임 설정 메뉴

### 메뉴 열기/닫기

**규칙 1. 메뉴 열기**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 2. 싱글플레이 일시정지**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 3. 포기 확인 팝업**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 게임 포기

**규칙 4. 포기 결과**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 5. 재경기**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

### 프로필 서브 패널

**규칙 6. 프로필 버튼 (서브 패널 열기/닫기)**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

## 로비 설정/프로필 UI

**규칙 1. ProfilePanel / SettingPanel 최상위 탭 분리**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.

**규칙 2. SettingPanel 사운드 탭 규칙 참조**

본문 원본: [GameSystemRules_SettingsUI.md](GameSystemRules_SettingsUI.md). 기존 제목·번호는 이전 참조를 위한 안내이며 규칙 사본을 두지 않는다.
