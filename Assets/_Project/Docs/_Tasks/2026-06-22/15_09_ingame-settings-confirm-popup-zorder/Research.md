# Research: 인게임 설정 - 게임포기 확인팝업 가림(Z-order) 버그

## 1. 한눈에 보는 설명 (자연어)

게임 중 우상단 설정 버튼을 누르면 "인게임 설정" 창이 뜹니다. 이 창에서 "게임포기"를
누르면 "정말 포기하시겠습니까?"라는 확인 창(ConfirmPopup)이 나타나야 합니다.

그런데 이 확인 창이 설정 창보다 **뒤쪽에 그려져서** 설정 창에 가려 보이지 않는 문제가
있습니다. 사용자는 포기 버튼을 눌렀는데도 아무 변화가 없는 것처럼 느끼게 됩니다.

이 문서는 "왜 확인 창이 설정 창 뒤에 그려지는가"를 코드와 씬/프리팹 파일을 직접 열어
근거를 가지고 규명하기 위한 조사 기록입니다.

조사 결과 핵심은 다음 한 문장입니다:
**확인 창(ConfirmPopup)은 자기 Canvas가 없어서 부모인 UIManager Canvas(SortingOrder=100)를
따라 그려지는데, 설정 창의 본체 "Panel"은 자기 Canvas로 SortingOrder=200을 가지므로,
숫자가 더 큰 설정 창이 항상 위에 그려진다.**

---

## 2. 조사 대상 및 근거 파일

- 코드
  - `Assets/_Project/Scripts/Presentation/UI/UIManager.cs`
  - `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs`
- 프리팹
  - `Assets/_Project/Prefabs/UI/ConfirmPopup.prefab`
- 씬
  - `Assets/_Project/Scenes/Login.unity` (UIManager 1회 생성 → DontDestroyOnLoad)
  - `Assets/_Project/Scenes/Game.unity` (InGameSettings 패널 배치)

---

## 3. 현재 코드/씬 상태 (검증된 사실)

### 3-1. ConfirmPopup의 SortingOrder = 100 (UIManager Canvas 상속)

- `ConfirmPopup.prefab`에는 **Canvas 컴포넌트(u!223)가 존재하지 않는다.**
  - 검증: `grep -cE 'm_OverrideSorting' ConfirmPopup.prefab` → **0건**.
    (파일에 나오는 `m_geometrySortingOrder`는 TMP_Text의 속성으로, Canvas의 SortingOrder와
    무관하다.)
- `Login.unity`에서 ConfirmPopup은 **"UIManager Canvas" 하위의 프리팹 인스턴스**로 배치됨.
  - "UIManager Canvas" GameObject(fileID 1123113271)의 Canvas는
    `m_OverrideSorting: 1`, `m_SortingOrder: 100`.
- 결론: ConfirmPopup은 독립 Canvas가 없으므로 **부모 Canvas의 SortingOrder=100으로 렌더링된다.**

### 3-2. InGameSettings 본체 "Panel"의 SortingOrder = 200 (독립 Canvas Override)

`Game.unity`의 인게임 설정 계층 구조(검증된 fileID 추적):

```
[UI] Canvas (root, GO 2087256513) ── Canvas SO=0, OverrideSorting=0
└─ SafeAreaContainer (GO 1259319714) ── Canvas 없음
   └─ InGameSettingsPanel (GO 1733172838)
      ├─ RectTransform (u!224, 1733172839)
      ├─ CanvasGroup   (u!225, 1733172841)
      └─ InGameSettingsUI 스크립트 (u!114, 1733172840)   ← Canvas 없음!
         └─ Panel (GO 697420764)   ← 설정 창 본체(박스)
            ├─ Canvas (u!223, 8000000005) ── OverrideSorting=1, **SortingOrder=200**
            ├─ GraphicRaycaster (8000000006)
            └─ AnimatedPanel (697420766)   ← InGameSettingsUI._panel 이 가리키는 대상
```

검증 포인트:
- `InGameSettingsPanel`(루트)은 컴포넌트가 RectTransform + CanvasGroup + 스크립트뿐 →
  **Canvas 없음**. 즉 루트 자체는 [UI] Canvas(SO=0)를 따른다.
- 실제 SO=200 Canvas는 한 단계 아래 자식 **"Panel"(GO 697420764)** 에 붙어 있다.
- `InGameSettingsUI._panel` 직렬화 값 = `{fileID: 697420766}` → 바로 이 "Panel" GO의
  AnimatedPanel 컴포넌트. 즉 설정 창 본체가 SO=200 Canvas를 가진 오브젝트와 동일하다.

씬에서 SortingOrder=200을 가진 Canvas는 총 5개이며 각각 다음 팝업 본체에 붙어 있다(검증):
BuildingPopup / BuildingActionPanel / **Panel(=InGameSettings 본체)** / GameEndPanel /
ProductionPopup.

### 3-3. ShowBlockingOverlay / ShowConfirm 호출 흐름

- `InGameSettingsUI.Show()` → `UIManager.Instance?.ShowBlockingOverlay(Hide)` 호출
  (BlockingOverlay는 UIManager Canvas=SO=100 소속, alpha/blocksRaycasts로 표시).
- 게임포기 버튼: `_forfeitButton.onClick → OnForfeitClicked()`
  → `UIManager.Instance?.ShowConfirm("정말 포기하시겠습니까?", OnForfeitConfirmed, null, "포기", "취소")`
  → `UIManager.ShowConfirm` → `_confirmPopup.Show(...)`.
- 즉 ConfirmPopup은 **UIManager Canvas(SO=100)** 안에서 표시된다.

---

## 4. 근본 원인 (Root Cause)

렌더 순서는 Canvas의 SortingOrder 숫자가 클수록 위에 그려진다.

| UI 요소 | 소속 Canvas | SortingOrder |
|---|---|---|
| ConfirmPopup (게임포기 확인창) | UIManager Canvas (상속) | **100** |
| BlockingOverlay (배경 차단) | UIManager Canvas (상속) | 100 |
| InGameSettings 본체 "Panel" | Panel 자체 Canvas Override | **200** |

ConfirmPopup(100) < InGameSettings Panel(200) 이므로,
**확인 창이 항상 설정 창 뒤에 그려진다.** 이것이 직접적·근본적 원인이다.

(참고: 버그 리포트의 "알려진 구조"에서 'InGameSettings 패널 SO=200'은 정확하며,
다만 그 200이 패널 루트가 아니라 한 단계 아래 자식 "Panel"의 Canvas에 있다는 점만
추가로 확인되었다. 결론에는 영향 없음.)

---

## 5. 해결 방향 (참고 — 구현은 별도 승인 후)

ConfirmPopup이 SO=200 패널들 위에 뜨려면 SortingOrder가 200보다 커야 한다. 후보:

- **A. ConfirmPopup에 독립 Canvas Override 부여 (예: SO=250)**
  - ConfirmPopup.prefab에 Canvas(OverrideSorting=1, SortingOrder=250) + GraphicRaycaster 추가.
  - 가장 직접적이고 다른 팝업(SO=200) 전부에 공통 적용됨. BlockingOverlay와의 상대 순서
    재점검 필요(오버레이는 ConfirmPopup보다 아래여야 하므로 100~249 사이면 OK이나,
    오버레이가 확인창을 같이 덮지 않도록 정렬 검토 필요).
- **B. UIManager Canvas 자체의 SortingOrder를 100 → 250 등으로 상향**
  - ConfirmPopup·BlockingOverlay·LoadingIndicator가 모두 함께 올라감. 단,
    LoadingIndicator(SO=300, 독립 Canvas)와의 상하 관계, 그리고 BlockingOverlay가 모든
    패널을 덮어버리는 부작용을 반드시 재검토해야 함.

> 두 방향 모두 BlockingOverlay/LoadingIndicator와의 상대 z-order에 영향을 주므로,
> SortingOrder 체계 전반(0/100/200/300)을 함께 검토한 뒤 구현 방향을 확정해야 한다.
> 구현은 game-programmer 위임 + 사용자 승인 후 진행한다.

---

## 6. 조사 중 발견한 부가 이슈

1. **문서상 SO 구조와 실제 위치의 미세 불일치**
   "SO=200: 각 패널 Canvas Override (… InGameSettings 등)"는 맞지만, InGameSettings의
   경우 200은 루트가 아니라 자식 "Panel"에 있다. 향후 SO 정리 시 혼동 가능 지점.

2. **BlockingOverlay 중첩 카운터(_blockingOverlayRefCount)와의 상호작용**
   `InGameSettingsUI.Show()`가 오버레이를 켠 상태에서 ConfirmPopup이 뜨는데,
   현재는 ConfirmPopup이 BlockingOverlay를 추가로 호출하는지 여부를 `ConfirmPopup.cs`에서
   별도 확인 필요(이번 조사 범위 밖). 만약 ConfirmPopup도 오버레이를 켠다면, 확인창을
   SO 상향했을 때 오버레이가 확인창을 덮는 새 문제가 생길 수 있어 함께 검증 권장.

3. **LoadingIndicator(SO=300)와의 우선순위**
   해결책 B(UIManager Canvas 상향) 채택 시 LoadingIndicator와의 상하 관계가 바뀔 수 있어
   해결책 선택 전 반드시 비교 검토 필요.
