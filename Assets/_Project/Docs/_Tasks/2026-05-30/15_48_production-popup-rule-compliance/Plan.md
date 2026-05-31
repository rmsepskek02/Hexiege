# Plan — ProductionPopup 규칙 준수 수정

## 작업 목적 (자연어 설명)

Research.md에서 발견한 규칙 위반 항목들을 수정한다.
**모든 수정은 현재 시각적 위치와 구조를 완전히 유지하면서 고정 픽셀값을 앵커 비율로 환산하는 작업이다.**
Inspector 설정 변경만 이루어지며 C# 코드 변경은 없다.
변경량이 많으므로 Editor 1회성 스크립트를 그룹별로 작성하여 사용자가 실행하는 방식으로 진행한다.

---

## ⚠️ 기존 로직 제거 규칙 적용

이번 작업은 Inspector 값 변경이므로 "기존 로직 제거" 항목은 없다.
단, Row2 (Slot7/8/9)는 사용되지 않는 잔존 오브젝트이므로 제거 대상으로 포함하나,
[6] 사용자 테스트 통과 후에 최종 삭제한다.

---

## 역산 계산 기준

기준 해상도: **1080 × 1920**
ProductionPanel 현재 실제 높이: **(0.4 × 1920) + 150 = 918px** (anchor 40% + sizeDelta 150px)
이 918px이 ProductionPanel 자식들의 부모 높이 기준이다.

Unity RectTransform 위치 계산식 (pivot=(0.5,0.5) 기준):
```
anchorPivotY = (anchorMin.y + (anchorMax.y - anchorMin.y) × pivot.y) × parentH
visualCenterY = anchorPivotY + anchoredPosition.y
visualHeight   = (anchorMax.y - anchorMin.y) × parentH + sizeDelta.y
visualBottom   = visualCenterY - visualHeight × 0.5
visualTop      = visualCenterY + visualHeight × 0.5
```

---

## 수정 그룹 분류

| 그룹 | 대상 | 위반 건수 | 근거 규칙 |
|------|------|----------|----------|
| A | ProductionPanel | 1건 | 규칙 2 |
| B | ProgressBar + Fill | 2건 | 규칙 2, 규칙 3 |
| C | QueueSlots + Slot1/2/3 + SlotImage | 3건 | 규칙 2 |
| D | InfoBar + 자식 | 3건 | 규칙 2 |
| E | BorderOverlay ×3 | 1건 | 규칙 2 |

---

## 그룹 A: ProductionPanel

### A-1. ProductionPanel sizeDelta → anchorMax.y 환산 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

**역산:**
```
현재 시각 범위: 0px ~ 918px
새 anchorMax.y = 918 / 1920 = 0.478
```

**현재 상태:**
```
anchorMin=(0,0)  anchorMax=(1, 0.4)
sizeDelta=(0, 150)  pivot=(0.5, 0)
```

**수정 후:**
```
anchorMin=(0,0)  anchorMax=(1, 0.478)
sizeDelta=(0, 0)  pivot=(0.5, 0)
```

---

## 그룹 B: ProgressBar + Fill

### B-1. ProgressBar 앵커 비율 환산 (규칙 2)

**근거:** 규칙 2 "sizeDelta, anchoredPosition 고정 픽셀값 사용 금지"

**역산 (부모 높이 918px):**
```
anchorPivotY = (0.17 + 0.04) × 918 = 192.78px
visualHeight = 0.08 × 918 + 100 = 173.44px
visualBottom = 192.78 + (-36) - 86.72 = 70.06px
visualTop    = 192.78 + (-36) + 86.72 = 243.50px

새 anchorMin.y = 70.06 / 918 = 0.076
새 anchorMax.y = 243.50 / 918 = 0.265
```

**현재 상태:**
```
anchorMin=(0, 0.17)  anchorMax=(1, 0.25)
pos=(0, -36)  sizeDelta=(0, 100)
```

**수정 후:**
```
anchorMin=(0, 0.076)  anchorMax=(1, 0.265)
pos=(0, 0)  sizeDelta=(0, 0)
```

---

### B-2. ProgressBar > Fill 앵커 비율 환산 (규칙 2, 규칙 3)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지", 규칙 3 "Filled 이미지 자식 앵커 비율 적용"

**역산 (부모 = ProgressBar: 1080 × 173.44px):**
```
X축:
  anchorPivotX = 540px
  visualWidth  = 1080 + (-300) = 780px
  visualLeft   = 540 - 390 = 150px  → anchorMin.x = 150/1080 = 0.139
  visualRight  = 540 + 390 = 930px  → anchorMax.x = 930/1080 = 0.861

Y축:
  anchorPivotY = 86.72px
  visualHeight = 173.44 + (-140) = 33.44px
  visualBottom = 86.72 - 16.72 = 70.00px  → anchorMin.y = 70.00/173.44 = 0.404
  visualTop    = 86.72 + 16.72 = 103.44px → anchorMax.y = 103.44/173.44 = 0.597
```

**현재 상태:**
```
anchorMin=(0, 0)  anchorMax=(1, 1)
pos=(0, 0)  sizeDelta=(-300, -140)
```

**수정 후:**
```
anchorMin=(0.139, 0.404)  anchorMax=(0.861, 0.597)
pos=(0, 0)  sizeDelta=(0, 0)
```

> ⚠️ Fill anchorMax.y=0.597이면 ProgressBar 중앙에 약 33px 높이의 Fill 영역이 그려진다.
> ProgressBar 스프라이트 프레임 두께에 따라 실기 확인 후 미세 조정이 필요할 수 있다.

---

## 그룹 C: QueueSlots

### C-1. QueueSlots anchoredPosition → anchorMin/Max 환산 (규칙 2)

**근거:** 규칙 2 "anchoredPosition 고정 픽셀값 사용 금지"

**역산 (부모 높이 918px):**
```
anchorPivotY = (0.25 + 0.12) × 918 = 339.66px
visualHeight = 0.24 × 918 + 0 = 220.32px
visualBottom = 339.66 + (-71) - 110.16 = 158.50px → anchorMin.y = 158.50/918 = 0.173
visualTop    = 339.66 + (-71) + 110.16 = 378.82px → anchorMax.y = 378.82/918 = 0.413
```

**현재 상태:**
```
anchorMin=(0, 0.25)  anchorMax=(1, 0.49)
pos=(0, -71)
```

**수정 후:**
```
anchorMin=(0, 0.173)  anchorMax=(1, 0.413)
pos=(0, 0)
```

---

### C-2. QueueSlots HLG 제거 (규칙 2)

**근거:** 규칙 2 "고정 픽셀 간격 사용 금지"

HLG (spacing=100, ctrl=(0,0), force=(0,0))를 **제거**한다.
슬롯을 직접 앵커 비율로 배치하므로 레이아웃 그룹이 불필요하다.

---

### C-3. QueueSlots > Slot1/2/3 직접 앵커 배치 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

HLG 제거 후 각 슬롯을 QueueSlots 기준으로 앵커 비율 직접 배치.

**역산 (QueueSlots: 1080 × 220.32px):**
```
3슬롯(160px each) + 2간격(100px each) = 680px → 좌우 여백 각 200px

슬롯 높이: (220.32 - 160) / 2 = 30.16px
  anchorMin.y = 30.16 / 220.32 = 0.137
  anchorMax.y = 190.16 / 220.32 = 0.863

Slot1: 좌=200px, 우=360px
  anchorMin=(200/1080, 0.137) = (0.185, 0.137)
  anchorMax=(360/1080, 0.863) = (0.333, 0.863)

Slot2: 좌=460px, 우=620px
  anchorMin=(460/1080, 0.137) = (0.426, 0.137)
  anchorMax=(620/1080, 0.863) = (0.574, 0.863)

Slot3: 좌=720px, 우=880px
  anchorMin=(720/1080, 0.137) = (0.667, 0.137)
  anchorMax=(880/1080, 0.863) = (0.815, 0.863)
```

**검증:**
- 슬롯 가로: (0.333-0.185) × 1080 = 0.148 × 1080 = 160px ✓
- 슬롯 세로: (0.863-0.137) × 220.32 = 0.726 × 220.32 ≈ 160px ✓

**모든 슬롯:** pos=(0, 0), sizeDelta=(0, 0)

---

### C-4. QueueSlots > SlotImage 앵커 비율화 (규칙 2)

**근거:** 규칙 2 "anchorMin=anchorMax 단일점 + sizeDelta 고정 금지"

**현재 상태:**
```
anchorMin=(0.5, 0.5)  anchorMax=(0.5, 0.5)
sizeDelta=(150, 150)
```

**역산 (Slot 160×160px 기준, SlotImage 150×150px, 여백 5px):**
```
anchorMin = 5 / 160 = 0.031
anchorMax = 155 / 160 = 0.969
```

**수정 후:**
```
anchorMin=(0.031, 0.031)  anchorMax=(0.969, 0.969)
pos=(0, 0)  sizeDelta=(0, 0)
```

검증: (0.969-0.031) × 160 = 0.938 × 160 = 150px ✓

---

## 그룹 D: InfoBar

### D-1. InfoBar 앵커 환산 — 자식 높이 100px 기준 (규칙 2)

**근거:** 규칙 2 "anchoredPosition 고정 픽셀값 사용 금지"

현재 자식(GoldIcon 등)이 100px로 InfoBar(82.62px)를 오버플로우 중.
자식 시각 크기(100px)를 유지하면서 앵커 기반 전환을 위해 InfoBar 높이를 100px에 맞게 조정한다.

**역산 (부모 918px, InfoBar 시각 중심=58.31px, 목표 높이=100px):**
```
visualBottom = 58.31 - 50 =  8.31px → anchorMin.y = 8.31/918 = 0.009
visualTop    = 58.31 + 50 = 108.31px → anchorMax.y = 108.31/918 = 0.118
```

**현재 상태:**
```
anchorMin=(0.1, 0)  anchorMax=(0.9, 0.09)
pos=(0, 17)
```

**수정 후:**
```
anchorMin=(0.1, 0.009)  anchorMax=(0.9, 0.118)
pos=(0, 0)
```

검증: (0.118-0.009) × 918 = 0.109 × 918 = 100.06px ≈ 100px ✓

---

### D-2. InfoBar HLG 제거 + 자식 직접 앵커 배치 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

QueueSlots와 동일한 방식으로 HLG를 제거하고 자식을 InfoBar 기준 앵커 비율로 직접 배치한다.
아이콘 너비와 높이가 모두 100px로 자연스럽게 1:1이 되므로 AspectRatioFitter 불필요.

**InfoBar HLG: 제거**

**역산 (InfoBar: 864 × 100px):**
```
GoldIcon : anchorMin=(0,      0) ~ anchorMax=(0.116, 1)  → 100×100px (1:1 자동)
GoldText : anchorMin=(0.116,  0) ~ anchorMax=(0.5,   1)  → 왼쪽 절반 텍스트 영역
PopIcon  : anchorMin=(0.5,    0) ~ anchorMax=(0.616, 1)  → 100×100px (1:1 자동)
PopText  : anchorMin=(0.616,  0) ~ anchorMax=(1.0,   1)  → 오른쪽 절반 텍스트 영역
```

계산 근거: 아이콘 너비 = 100 / 864 = 0.116, 좌우 절반 기준 0.5

**모든 자식 공통:**
```
pos=(0, 0), sizeDelta=(0, 0)
LayoutElement: 제거 (앵커 배치로 불필요)
```

---

## 그룹 E: BorderOverlay ×3

### E-1. BorderOverlay pos/sizeDelta 정리 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

**현재 상태 (3개):**
```
anchorMin=(0,0)  anchorMax=(1,1)  ignoreLayout=1
pos=(-284.66,0) / (-1,0) / (290,0)
sizeDelta=(-569.32, 0)
sprite=NONE
```

**수정 후:**
```
anchorMin=(0,0)  anchorMax=(1,1)
pos=(0,0)  sizeDelta=(0,0)
```

anchor (0,0)~(1,1)이면 pos=(0,0), sizeDelta=(0,0)으로 부모를 완전히 채운다.
sprite=NONE이므로 시각적 변화 없음.

---

## 구현 방식

| 스크립트 | 담당 그룹 | 메뉴 경로 |
|----------|----------|----------|
| `FixProductionPopupGroupA.cs` | 그룹 A (ProductionPanel) | `Hexiege/Fix/ProductionPopup/GroupA` |
| `FixProductionPopupGroupB.cs` | 그룹 B (ProgressBar+Fill) | `Hexiege/Fix/ProductionPopup/GroupB` |
| `FixProductionPopupGroupC.cs` | 그룹 C (QueueSlots) | `Hexiege/Fix/ProductionPopup/GroupC` |
| `FixProductionPopupGroupD.cs` | 그룹 D (InfoBar) | `Hexiege/Fix/ProductionPopup/GroupD` |
| `FixProductionPopupGroupE.cs` | 그룹 E (BorderOverlay) | `Hexiege/Fix/ProductionPopup/GroupE` |

### 실행 순서

1. 그룹 A 실행 → 씬 저장 → 패널 전체 크기 확인
2. 그룹 B 실행 → 씬 저장 → ProgressBar/Fill 시각 확인
3. 그룹 C 실행 → 씬 저장 → QueueSlots 레이아웃 확인
4. 그룹 D 실행 → 씬 저장 → InfoBar 아이콘/텍스트 확인
5. 그룹 E 실행 → 씬 저장 → BorderOverlay 확인
6. 에디터 Play Mode에서 배럭 클릭 → 생산 패널 전체 실기 확인

### 주의 사항

- 각 스크립트 실행 전 `Game.unity`가 열려 있어야 한다
- 실행 후 씬을 반드시 저장(Ctrl+S)해야 한다
- 그룹 A를 가장 먼저 실행해야 자식 요소들의 부모 높이가 올바르게 반영된다
- 그룹 B Fill 앵커는 스프라이트 실제 두께에 따라 미세 조정 필요
- 그룹 C 슬롯 정사각형 유지는 이번 작업 범위 외 (AspectRatioFitter 별도 작업)

---

## 위험 요소

| 항목 | 위험 | 대응 |
|------|------|------|
| 그룹 A 먼저 실행 필수 | 자식들 앵커 계산이 918px 기준 — A 이전에 다른 그룹 실행 시 부모 크기 불일치 | 실행 순서 반드시 A→B→C→D→E |
| ProgressBar Fill | 스프라이트 프레임 두께에 따라 anchorMin/Max.y 오차 가능 | 실기 확인 후 수동 미세 조정 |
| QueueSlots 슬롯 (C-3) | HLG 제거 후 슬롯이 앵커 배치로 전환 — UnitImage 참조 등 기존 코드와 충돌 없는지 확인 | 실기 확인 후 UnitImage 슬롯 인식 테스트 |
| InfoBar 높이 조정 (D-1) | anchorMax.y 변경으로 InfoBar가 다른 요소와 겹칠 수 있음 | 실기 확인 후 anchorMin/Max.y 미세 조정 |

---

## 참고 파일

| 파일 | 용도 |
|------|------|
| `Assets/_Project/Docs/GameSystemRules.md` | UI 규칙 원본 |
| `Assets/_Project/Docs/_Tasks/2026-05-30/15_48_production-popup-rule-compliance/Research.md` | 위반 항목 실측 데이터 |
