# Research: Responsive Popup UI

## 날짜: 2026-03-09

## 문제
ProductionPopup / BuildingPopup에서 유닛/건물 선택 버튼이 실제 기기에서 패널 테두리를 침범함.

## CanvasScaler
- Scale With Screen Size
- ReferenceResolution: 540 × 960
- ScreenMatchMode: Match Width Or Height
- MatchWidthOrHeight: 0.5
  → 세로가 긴 기기(9:20 등)에서 캔버스 너비가 540px 미만이 됨

## 오브젝트 구조

### ProductionPopup → ProductionPanel
- AnchorMin=(0,0), AnchorMax=(1,0) → 하단 앵커, 전체 너비
- SizeDelta=(0, 500) → 고정 높이 500px
- 배경: Simple Image (wooden panel)

#### VerticalButtons (child of ProductionPanel)
- AnchorMin=(0,0.5), AnchorMax=(1,0.5) → 수평 stretch
- SizeDelta=(-160, 100) → 양쪽 80px 여백, 고정 높이 100px
- VerticalLayoutGroup: ChildControlWidth=1, ChildControlHeight=0, Spacing=0

##### UnitButtons1 / UnitButtons2 (rows)
- SizeDelta=(450, 100) → VerticalLayoutGroup이 너비를 (캔버스-160)으로 강제
- HorizontalLayoutGroup: ChildControlWidth=0, ChildForceExpandWidth=0, Spacing=0
- 자식: Slot1/2/3 각 SizeDelta=(150, 100) → 3개 합계 450px (고정)

### BuildingPopup → BuildingPanel
- 동일한 구조 (AnchorMin=(0,0), AnchorMax=(1,0), SizeDelta=(0,500))

#### BuildingButtons (child of BuildingPanel)
- AnchorMin=(0,0.5), AnchorMax=(1,0.5)
- SizeDelta=(-160, 100)
- VerticalLayoutGroup: ChildControlWidth=1, ChildControlHeight=0

##### BuildingButtons1/2/3 (rows)
- SizeDelta=(450, 100) → 동일 구조
- HorizontalLayoutGroup: ChildControlWidth=0, Spacing=0
- 자식: 건물 버튼 3개 × 150px (고정)

## 근본 원인
- VerticalLayoutGroup이 각 행 너비를 (캔버스너비-160)으로 강제
- HorizontalLayoutGroup 내부에서 자식 너비 미제어 (ChildControlWidth=0)
- 슬롯 3개 × 150px = 450px 고정 → 컨테이너 너비가 좁아지면 테두리 침범

## 슬롯 내부 구조 (Slot1 기준, Production)
- UnitImage: AnchorMin=(0,1), AnchoredPosition=(55,-50), SizeDelta=(80,80) → 고정 위치/크기
- GoldText: AnchorMin=(0.5,0.5), AnchoredPosition=(40,-15), SizeDelta=(40,20) → 고정
- GoldIcon: 고정 위치
- AutoIndicator: 고정 위치

## 결론
슬롯 너비 제어권을 HorizontalLayoutGroup에 넘기고 (ChildControlWidth=1),
AspectRatioFitter(WidthControlsHeight)로 슬롯 비율 유지,
내부 요소를 stretch 앵커로 변경하면 완전한 반응형 구현 가능.
