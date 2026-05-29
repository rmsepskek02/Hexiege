# Plan — BuildingActionPanelUI 재설계

## 작업 목적

BuildingActionPanel의 레이아웃을 GameSystemRules에 맞게 재구성합니다.
래퍼의 고정 픽셀 오프셋 제거, 불필요한 ProductionPanelUI 잔재 계층 정리,
GoldText 앵커 오류 수정, CancelButton 위치를 다른 패널과 통일하는 것이 핵심입니다.

또한 빈 슬롯 숨김 처리를 에디터 고정값이 아닌 런타임에서 처리하도록 설계합니다.
이를 통해 나중에 건물 타입별로 다른 버튼을 표시하는 확장도 자연스럽게 지원됩니다.

---

## 목표 계층 구조

```
BuildingActionPanel (래퍼 + BuildingActionPanelUI + AnimatedPanel)
  anchorMin=(0,0), anchorMax=(1,1), pos=(0,0), delta=(0,0)
  └── BuildingPanel (패널 프레임 Image)
        anchorMin=(0,0), anchorMax=(1,0.4), pos=(0,0), delta=(0,0)
        ├── CancelButton
        │     anchorMin=(0.883,0.852), anchorMax=(0.993,0.97), pos=(0,0), delta=(0,0)
        │     ← BuildingPlacementUI와 동일한 위치
        └── ContentArea (새 컨테이너)
              anchorMin=(0.05, 0.05), anchorMax=(0.82, 0.95), pos=(0,0), delta=(0,0)
              VerticalLayoutGroup:
                childControlHeight=true, childForceExpandHeight=true
                childControlWidth=true, childForceExpandWidth=true
                spacing=10, padding=10
              ├── HeaderText (TMP)
              │     LayoutElement: flexibleHeight=3 (상단 영역 60%)
              └── DemolishArea (HorizontalLayoutGroup)
                    LayoutElement: flexibleHeight=2 (하단 영역 40%)
                    childControlWidth=true, childForceExpandWidth=true
                    spacing=8
                    ├── DestroyButton
                    │     LayoutElement: flexibleWidth=1
                    └── GoldText (TMP)
                          LayoutElement: flexibleWidth=1
                          anchorMax.x 1.1 → 앵커 제거 후 LayoutElement로 크기 제어
```

---

## 수정 항목

### 1. Game.unity — 씬 계층 재구성

**근거**: GameSystemRules Rule 2 (앵커 기반 배치), Rule 4 (SafeArea)

#### 1-1. BuildingActionPanel (래퍼) 오프셋 제거
- 현재: anchoredPosition=(0,-75), sizeDelta=(0,-150)
- 목표: anchoredPosition=(0,0), sizeDelta=(0,0)

#### 1-2. 불필요한 계층 제거
아래 오브젝트들을 씬에서 삭제한다:
- `UnitsButtons` (VerticalLayoutGroup 컨테이너)
- `UnitButtons` (HorizontalLayoutGroup 컨테이너)
- `Buttons` (HorizontalLayoutGroup 컨테이너)
- `UnitButtons` 내 `DestroyButton`을 제외한 미사용 버튼 전체
- `Buttons` 내 버튼 3개 전체

> ⚠️ 기존 로직 제거 주의: DestroyButton, GoldText는 `_demolishButton`, `_demolishRefundText`로 Inspector 연결된 실사용 요소이므로 삭제하지 않고 위치만 이동한다.

#### 1-3. HeaderText 앵커 재구성
- 현재: anchorMin=(0,0.5), anchorMax=(1,0.5), anchoredPosition=(-25,210), sizeDelta=(-150,50)
- 목표: ContentArea의 VerticalLayoutGroup child로 편입, LayoutElement로 비율 제어

#### 1-4. GoldText 앵커 수정
- 현재: anchorMax.x=1.1 (1.0 초과 — 부모 경계 밖)
- 목표: DemolishArea HLG child로 편입, anchoredPosition=0, sizeDelta=0

#### 1-5. CancelButton 위치 통일
- 현재: anchorMin=(0.87,0.78), anchorMax=(1,1), pos=(0,0), delta=(0,0)
- 목표: anchorMin=(0.883,0.852), anchorMax=(0.993,0.97), pos=(0,0), delta=(0,0)
  (BuildingPlacementUI와 동일 — GameSystemRules 팝업 일관성)

---

### 2. 1회성 Editor 스크립트 작성

**파일**: `Assets/Editor/RebuildBuildingActionPanel.cs`
**메뉴**: `Hexiege/Setup/BuildingActionPanel 재구성`

구현 내용:
- BuildingActionPanel 래퍼 anchoredPosition/sizeDelta 초기화
- 불필요한 오브젝트(UnitsButtons, UnitButtons, Buttons, 미사용 버튼들) Undo 등록 후 제거
- ContentArea 오브젝트 신규 생성 (VerticalLayoutGroup)
- DestroyButton, GoldText를 ContentArea 하위 DemolishArea(HLG)로 이동
- HeaderText를 ContentArea 하위로 이동 및 앵커 재설정
- CancelButton 앵커 조정 (BuildingPlacementUI와 통일)
- Inspector 필드 재연결 확인 (_demolishButton, _demolishRefundText, _headerText, _cancelButton)

---

## Inspector 확인 목록

스크립트 실행 후 BuildingActionPanelUI 컴포넌트에서 아래를 확인:

| 필드 | 연결 대상 |
|------|-----------|
| `_popup` | BuildingActionPanel (AnimatedPanel) |
| `_sharedBackground` | 공유 SharedBackgroundButton |
| `_headerText` | ContentArea 하위 HeaderText |
| `_cancelButton` | CancelButton |
| `_demolishButton` | DemolishArea 하위 DestroyButton |
| `_demolishRefundText` | DemolishArea 하위 GoldText |

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| DestroyButton 이동 시 Inspector 연결 끊김 | 오브젝트를 다른 부모로 이동하면 SerializedObject 참조는 유지되나 확인 필요 | 스크립트 실행 후 Inspector에서 _demolishButton 연결 상태 확인 |
| GoldText 앵커 수정 후 위치 변화 | anchorMax.x=1.1 제거 시 텍스트가 기존과 다른 위치에 나타날 수 있음 | 실기기에서 환불 금액 텍스트 표시 위치 확인 필요 |
| HeaderText 텍스트 잔류 | 현재 "asdasd..." 테스트 텍스트가 남아있음 | 런타임에 BuildingData.Type으로 덮어써지므로 기능 영향 없음 |

---

---

## 런타임 슬롯 숨김 설계 (추가 결정사항)

### 배경

빈 슬롯 8개의 CanvasGroup.alpha=0을 에디터에서 고정하는 방식은 다음 문제가 있다:
- 나중에 건물 타입별로 다른 버튼을 표시하는 확장이 어렵다
- 에디터 값에 의존하면 스크립트 재실행 시마다 다시 설정해야 한다

### 설계 방향 — BuildingPlacementUI와 동일한 패턴

BuildingPlacementUI의 슬롯 관리 방식을 그대로 따른다.

**BuildingPlacementUI 패턴 요약:**
- `Initialize()`: `_buildingButtons` 전체를 순회해 CanvasGroup 캐시, 초기값 전부 alpha=0
- `Show()`: 실제 사용할 버튼 리스트와 비교해 i < list.Count이면 alpha=1, 아니면 alpha=0

**BuildingActionPanelUI 적용:**

```
Inspector 필드 2개 추가 (BuildingActionPanelUI.cs):
  [SerializeField] private List<Button> _allSlotButtons
    → 9개 슬롯 전부 연결 (전체 슬롯 CanvasGroup 제어용)
  [SerializeField] private List<Button> _activeSlotButtons
    → 실제 사용할 버튼만 연결 (현재: DestroyButton 1개)
    → Inspector에서 추가/제거로 활성 슬롯 조절 가능

Initialize() 추가 로직:
  → _allSlotButtons 전체에 CanvasGroup 캐시, 초기값 alpha=0

OnShow(BuildingData) 오버라이드:
  → _allSlotButtons 전체 alpha=0 초기화
  → _activeSlotButtons에 포함된 버튼만 alpha=1 활성화
```

### 구현 위치

- **파일**: `Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs`
- **방법**: `Initialize()` 오버라이드(또는 확장) + `BuildingPanelBase`의 `OnShow(BuildingData)` 훅 오버라이드

### 미래 확장 시나리오

건물 타입에 따라 다른 버튼을 표시해야 할 때,
`OnShow()` 내부에서 `building.Type`을 확인하고 `_activeSlotButtons`를 동적으로 구성하거나,
Inspector에서 타입별 활성 버튼 리스트를 별도로 정의하면 된다.

---

## 작업 순서

1. `Assets/Editor/RebuildBuildingActionPanel.cs` 작성 (3x3 그리드 구조)
   - 에디터 스크립트에서는 모든 슬롯을 alpha=1로 생성 (런타임에서 제어)
2. Unity에서 `Hexiege/Setup/BuildingActionPanel 재구성` 메뉴 실행
3. `BuildingActionPanelUI.cs`에 `OnShow()` 오버라이드 구현
   - 전체 슬롯 숨김 후 DestroyButton 슬롯만 활성화
4. Inspector 연결 상태 확인
5. 에디터 플레이 모드에서 채굴소 탭 → 팝업 표시 확인
6. 실기기 TC-SINGLE-BAP-001 재검증
