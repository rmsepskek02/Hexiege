# Plan: Responsive Popup UI

## 날짜: 2026-03-09

## 목표
ProductionPopup / BuildingPopup의 버튼 슬롯이 어떤 해상도에서도
패널 테두리 내부에 위치하고, 슬롯 비율과 내부 요소 비율을 유지

---

## 변경 대상 오브젝트

### Production (VerticalButtons → UnitButtons1, UnitButtons2)
- UnitButtons1 HorizontalLayoutGroup
- UnitButtons2 HorizontalLayoutGroup
- Slot1, Slot2, Slot3 (UnitButtons1 자식)
- Slot1, Slot2, Slot3 (UnitButtons2 자식)
- 각 슬롯 내부 요소 (UnitImage, GoldText, GoldIcon, AutoIndicator)

### Building (BuildingButtons → BuildingButtons1, 2, 3)
- BuildingButtons1/2/3 HorizontalLayoutGroup
- 각 행의 Building 버튼 3개 × 3행
- 각 슬롯 내부 요소

---

## 변경 내용

### Step 1: 각 버튼 행 (UnitButtons/BuildingButtons 행)
```
HorizontalLayoutGroup:
  ChildControlWidth:    0 → 1
  ChildForceExpandWidth: 0 → 1
  ChildControlHeight:   0 (유지)
  Spacing: 8
  Padding Left/Right: 8
```

### Step 2: VerticalButtons / BuildingButtons 컨테이너 패딩
```
VerticalLayoutGroup:
  Padding Left/Right: 20  ← 패널 테두리 내부 여백 확보
  Spacing: 8
```

### Step 3: 각 슬롯에 AspectRatioFitter 추가
```
AspectRatioFitter:
  AspectMode: WidthControlsHeight
  AspectRatio: 1.5  (현재 150/100 비율 유지)
```

### Step 4: 슬롯 내부 이미지 (UnitImage / BuildingImage)
```
RectTransform:
  AnchorMin: (0.05, 0.15)  → stretch 기반 상대 위치
  AnchorMax: (0.85, 0.90)
  SizeDelta: (0, 0)
  AnchoredPosition: (0, 0)
```

### Step 5: 슬롯 내부 텍스트 (GoldText, PopText 등)
```
RectTransform:
  AnchorMin: (0.55, 0.05)
  AnchorMax: (1.0, 0.35)
  SizeDelta: (0, 0)
TMP: Enable Auto Size (min=8, max=24)
```

### Step 6: 슬롯 내부 아이콘 (GoldIcon, StarIcon 등)
```
RectTransform:
  AnchorMin: (0.35, 0.05)
  AnchorMax: (0.65, 0.35)
  SizeDelta: (0, 0)
```

---

## 예상 결과

| 캔버스 너비 | 슬롯 너비 | 슬롯 높이 |
|-----------|---------|---------|
| 540px     | ~156px  | ~104px  |
| 490px     | ~140px  | ~93px   |
| 410px     | ~115px  | ~77px   |

- 항상 1.5:1 비율 유지
- 내부 요소 비례 축소
- 테두리 침범 없음

---

## 변경 파일
- `Assets/_Project/Scenes/Game.unity` (UI RectTransform, LayoutGroup, AspectRatioFitter)
