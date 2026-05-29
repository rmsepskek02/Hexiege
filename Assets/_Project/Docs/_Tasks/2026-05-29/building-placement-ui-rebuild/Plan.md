# Plan — BuildingPlacementUI 재설계

## 작업 목적

BuildingPlacementUI의 레이아웃을 처음부터 재구성합니다.
기존 수치를 조정하는 방식이 아니라, GameSystemRules의 반응형 규칙을 정확히 따르는 구조로 씬 계층을 다시 구성하고 필요한 스크립트 필드를 추가합니다.

---

## 최종 계층 구조 (목표)

```
BuildingPopup (AnimatedPanel + BuildingPlacementUI)
  anchorMin={0,0}, anchorMax={1,1} ← 전체화면 래퍼 (기존 유지)
  └── BuildingPanel (패널 프레임 이미지)
        anchorMin={0,0}, anchorMax={1,0.4} ← SafeArea 하단 40%
        ├── CancelButton (X 이미지 버튼)
        │     anchorMin={0.85,0.8}, anchorMax={1,1} ← 우상단 고정
        └── GridContainer
              anchorMin과 anchorMax로 CancelButton 아래 나머지 영역 채움
              VerticalLayoutGroup
                Control Child Size Height = true
                Child Force Expand Height = true
                Spacing = [행 간격]
                Padding = [프레임 내부 여백]
              ├── Row0 (HorizontalLayoutGroup)
              │     Control Child Size Width = true
              │     Child Force Expand Width = true
              │     Spacing = [열 간격]
              │     ├── Button[0]
              │     ├── Button[1]
              │     └── Button[2]
              ├── Row1 (HorizontalLayoutGroup)
              │     ├── Button[3]
              │     ├── Button[4]
              │     └── Button[5]
              └── Row2 (HorizontalLayoutGroup)
                    ├── Button[6]
                    ├── Button[7]
                    └── Button[8]
```

---

## 버튼 내부 구조 (목표)

```
Button (CanvasGroup + Button)
  HorizontalLayoutGroup
  Child Force Expand Width = true
  ├── IconImage (건물 아이콘)
  │     앵커: 좌측 영역 차지
  │     Aspect Ratio Fitter 또는 LayoutElement로 비율 고정 검토
  └── CostContainer
        VerticalLayoutGroup
        Child Alignment = Middle Center
        ChildForceExpand = false
        ├── GoldIcon (Image, 고정 비율 크기)
        └── CostText (TextMeshProUGUI)
```

- 좌/우 비율은 IconImage : CostContainer = 약 6:4 으로 시작하여 실기 확인 후 조정
- GoldIcon과 CostText는 CostContainer 안에서 세로로 쌓이고, 전체 그룹이 버튼 우측 영역 세로 중앙에 위치

---

## 수정 항목

### 1. Game.unity — 씬 계층 재구성

**근거**: GameSystemRules Rule 2 (앵커 기반 배치), Rule 4 (SafeArea), Rule 5 (CanvasGroup 숨김)

- BuildingPanel의 anchorMin/Max를 SafeArea 하단 40%로 설정
- GridContainer에 VerticalLayoutGroup 구성 (Control Child Size + Force Expand)
- Row0/Row1/Row2 오브젝트 생성, 각각 HorizontalLayoutGroup 구성
- 각 Row에 Button 3개씩 배치 (기존 버튼 재활용 또는 재생성)
- 각 Button 내부: HorizontalLayoutGroup → IconImage + CostContainer(VerticalLG → GoldIcon + CostText)
- CancelButton 위치: anchorMin/Max로 우상단 고정 재조정

### 2. BuildingPlacementUI.cs — 골드 아이콘 참조 추가

**근거**: 버튼 내부 골드 아이콘은 현재 코드에 Inspector 참조가 없음

```csharp
[Header("Gold Icons")]
[Tooltip("각 버튼의 골드 아이콘 Image 리스트. _buildingButtons와 1:1 매칭.")]
[SerializeField] private List<Image> _buildingGoldIcons;
```

> ⚠️ 현재 동작 로직(Show/Close/UpdateCostTextColors 등)은 변경 없음. 참조 필드만 추가.

### 3. GameSystemRules.md — Rule 2 보완

**근거**: Layout Group 내부에서 반응형을 달성하는 구체적인 방법이 Rule 2에 명시되어 있지 않음

아래 내용을 Rule 2 하단에 추가:

```
Layout Group 내부 반응형 패턴:
GridLayoutGroup의 CellSize는 고정 픽셀값이므로 사용하지 않는다.
대신 VerticalLayoutGroup + HorizontalLayoutGroup 중첩 구조로 구성하고,
Control Child Size + Child Force Expand를 활성화하면 CellSize 없이
가용 공간을 자동으로 균등 분배한다.
```

---

## Inspector 재연결 목록

계층 재구성 후 BuildingPlacementUI 컴포넌트에 아래 필드를 다시 연결해야 합니다.

| 필드 | 연결 대상 |
|------|-----------|
| `_popup` | BuildingPopup (AnimatedPanel) |
| `_sharedBackground` | SharedBackgroundButton |
| `_cancelButton` | CancelButton |
| `_buildingButtons[0~8]` | Row0→버튼3개, Row1→버튼3개, Row2→버튼3개 순서로 |
| `_buildingButtonIcons[0~8]` | 각 버튼 내부 IconImage 순서대로 |
| `_buildingCostTexts[0~8]` | 각 버튼 내부 CostText 순서대로 |
| `_buildingGoldIcons[0~8]` | 각 버튼 내부 GoldIcon 순서대로 (신규) |

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| Inspector 연결 순서 오류 | 버튼 인덱스와 건물 매핑이 어긋나면 잘못된 건물이 배치됨 | Row별 순서(0→1→2→3→…→8)를 명확히 지키고 플레이 모드에서 즉시 확인 |
| CanvasGroup 초기화 타이밍 | Initialize()에서 CanvasGroup 캐시 구성 시 버튼 GameObject가 비활성이면 캐시 누락 가능 | 기존 코드는 이미 이 케이스를 처리하고 있음 (Initialize에서 직접 처리) |
| 패널 40% 높이 실기 확인 | SafeArea 기준 40%가 실제 기기에서 의도한 크기인지 확인 필요 | 에디터 Portrait 모드 + 실기기 양쪽에서 검증 |

---

## 작업 순서

1. GameSystemRules.md Rule 2 보완 텍스트 추가
2. BuildingPlacementUI.cs에 `_buildingGoldIcons` 필드 추가
3. Game.unity 씬 계층 재구성
   - BuildingPanel 앵커 수정
   - GridContainer + Row0/1/2 생성
   - 각 버튼 내부 구조 재구성
   - CancelButton 위치 재조정
4. Inspector 전체 재연결
5. 에디터 플레이 모드 확인

---

## 구현 완료 내용 (2026-05-29)

### 실제 적용된 씬 계층 (최종 확인 기준)

```
BuildingPopup (AnimatedPanel + BuildingPlacementUI)
  anchorMin=(0,0), anchorMax=(1,1)
  └── BuildingPanel
        anchorMin=(0,0), anchorMax=(1,0.4), anchoredPosition=(0,0), sizeDelta=(0,0)
        ├── CancelButton
        │     anchorMin=(0.883,0.852), anchorMax=(0.993,0.97), pos=(0,0), delta=(0,0)
        └── GridContainer
              anchorMin=(0.08,0.123), anchorMax=(0.92,0.864), pos=(0,0), delta=(0,0)
              VLG: padding=20, spacing=8, controlWidth/Height=true, forceExpand=true
              ├── Row0 (HLG: spacing=8, controlWidth/Height=true, forceExpand=true)
              │     ├── Button[0] (HLG: childControlWidth=true, forceExpand=true)
              │     │     ├── IconImage (LayoutElement: flexibleWidth=6)
              │     │     └── CostContainer (LayoutElement: flexibleWidth=4)
              │     │           VLG: spacing=4, controlHeight=true, forceExpandHeight=false
              │     │           ├── GoldIcon (ui_icon_gold, LE: min/preferred=44, flex=0)
              │     │           └── CostText (Maplestory Light SDF, LE: preferred=400/22)
              │     ├── Button[1] (동일 구조)
              │     └── Button[2] (동일 구조)
              ├── Row1 (동일 구조)
              └── Row2 (동일 구조)
```

### 변경된 파일
| 파일 | 변경 내용 |
|------|-----------|
| `Game.unity` | 씬 계층 전면 재구성 (BuildingButtons 구 컨테이너 제거 포함) |
| `BuildingPlacementUI.cs` | `_buildingGoldIcons` 필드 추가 |
| `GameSystemRules.md` | Rule 2 Layout Group 반응형 패턴 보완 |
| `Assets/Editor/RebuildBuildingPlacementUI.cs` | 1회성 셋업 스크립트 (재실행용으로 보존) |

### GameSystemRules 준수 검증 결과 (2026-05-29)
| 규칙 | 결과 |
|------|------|
| Rule 2 (앵커 기반) | ✅ 모든 오브젝트 순수 앵커 기반. GoldIcon/CostText preferredHeight는 Canvas Scaler로 비례 스케일되므로 허용 |
| Rule 4 (SafeArea) | ✅ BuildingPopup이 SafeAreaContainer 직속 자식 |
| Rule 5 (CanvasGroup) | ✅ 버튼 빈 슬롯 CanvasGroup alpha=0 사용. AnimatedPanel의 SetActive는 Layout Group 외부이므로 실질 문제 없음 |
| Rule 6 (폰트) | ✅ CostText = Maplestory Light SDF 확인 |

### 실기 테스트 필요 항목
- TC-SINGLE-BP-001, TC-SINGLE-BP-002: 에디터 재구성 완료. 실기기 재검증 필요
