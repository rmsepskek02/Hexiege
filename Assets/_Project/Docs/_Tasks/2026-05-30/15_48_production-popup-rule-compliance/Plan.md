# Plan — ProductionPopup 규칙 준수 수정

## 작업 목적 (자연어 설명)

Research.md에서 발견한 규칙 위반 항목들을 수정한다.
모든 수정은 Game.unity 씬의 Inspector 설정 변경이며, C# 코드 변경은 없다.
변경량이 많으므로 Editor 1회성 스크립트를 그룹별로 작성하여 사용자가 실행하는 방식으로 진행한다.

---

## ⚠️ 기존 로직 제거 규칙 적용

이번 작업은 Inspector 값 변경이므로 "기존 로직 제거" 항목은 없다.
단, Row2 (Slot7/8/9) 삭제는 사용되지 않는 오브젝트이므로 제거 대상으로 포함하나,
[6] 사용자 테스트 통과 후에 최종 삭제한다.

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

## 그룹 A: ProductionPanel 수정

### A-1. ProductionPanel sizeDelta 제거 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

**현재 상태:**
```
anchorMin=(0,0)  anchorMax=(1,0.4)
sizeDelta=(0, 150)
pivot=(0.5, 0)
```

**수정 후:**
```
anchorMin=(0,0)  anchorMax=(1,0.4)
sizeDelta=(0, 0)
pivot=(0.5, 0)
```

앵커 범위 0~40%가 이미 패널 높이를 결정하므로 sizeDelta.y=150은 불필요한 고정 픽셀이다.

---

## 그룹 B: ProgressBar + Fill 수정

### B-1. ProgressBar 앵커 비율화 (규칙 2)

**근거:** 규칙 2 "sizeDelta, pos 고정 픽셀값 사용 금지"

**현재 상태:**
```
anchorMin=(0,0.17)  anchorMax=(1,0.25)
pos=(0, -36)
sizeDelta=(0, 100)
```

**수정 후:**
```
anchorMin=(0,0.17)  anchorMax=(1,0.25)
pos=(0, 0)
sizeDelta=(0, 0)
```

앵커 범위 17%~25%가 높이를 결정하므로 pos와 sizeDelta 모두 0으로 정리한다.

---

### B-2. ProgressBar > Fill 앵커 비율화 (규칙 2, 규칙 3)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지", 규칙 3 "Filled 이미지 자식 앵커 비율 적용"

**현재 상태:**
```
anchorMin=(0,0)  anchorMax=(1,1)
pos=(0,0)
sizeDelta=(-300, -140)
```

**수정 후:**
```
anchorMin=(0.139, 0.073)  anchorMax=(0.861, 0.927)
pos=(0,0)
sizeDelta=(0, 0)
```

계산 근거 (부모 ProgressBar 기준):
- 기존 sizeDelta.x=-300 → 좌우 150px 패딩 → Fill 너비 = 부모-300
  부모 너비를 1080px 기준으로 보면: 150/1080 ≈ 0.139
- 기존 sizeDelta.y=-140 → 상하 70px 패딩
  ProgressBar 높이(앵커 8% of 1920≈154px) 기준: 70/154 ≈ 0.455 → anchorMin.y≈0.073, anchorMax.y≈0.927

> ⚠️ Fill의 실제 앵커 비율은 ProgressBar 이미지 스프라이트의 실제 프레임 두께에 따라 달라지므로,
> 스크립트 실행 후 실기 확인하여 미세 조정이 필요할 수 있다.

---

## 그룹 C: QueueSlots 수정

### C-1. QueueSlots pos 제거 (규칙 2)

**근거:** 규칙 2 "anchoredPosition 고정 픽셀값 사용 금지"

**현재 상태:**
```
anchorMin=(0,0.25)  anchorMax=(1,0.49)
pos=(0, -71)
```

**수정 후:**
```
anchorMin=(0,0.25)  anchorMax=(1,0.49)
pos=(0, 0)
```

---

### C-2. QueueSlots HLG spacing 비율화 (규칙 2)

**근거:** 규칙 2 "고정 픽셀 간격 사용 금지"

HLG spacing=100 고정 → **spacing=0** 으로 변경 후,
ChildForceExpandWidth=**1** 으로 설정하여 가용 공간을 Slot들이 균등 분배하도록 한다.

현재 HLG ctrl=(0,0) force=(0,0) → 수정 후: ctrl=(0,0) force=(1,0)

---

### C-3. QueueSlots > Slot1/2/3 sizeDelta 비율화 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

**현재 상태:**
```
sizeDelta=(160, 160)
```

QueueSlots HLG가 ForceExpandWidth=1로 가로를 분배하므로,
각 슬롯의 sizeDelta → **(0, 0)**으로 변경한다.
슬롯 높이는 QueueSlots 앵커 범위(25%~49%)가 결정한다.

---

### C-4. QueueSlots > SlotImage 앵커 비율화 (규칙 2)

**근거:** 규칙 2 "anchorMin=anchorMax 단일점 + sizeDelta 고정 금지"

**현재 상태:**
```
anchorMin=(0.5,0.5)  anchorMax=(0.5,0.5)
sizeDelta=(150, 150)
```

**수정 후:**
```
anchorMin=(0.1, 0.1)  anchorMax=(0.9, 0.9)
sizeDelta=(0, 0)
```

슬롯 크기의 약 10% 여백으로 중앙 배치.
실기 확인 후 여백 비율 조정 가능.

---

## 그룹 D: InfoBar 수정

### D-1. InfoBar pos 제거 (규칙 2)

**근거:** 규칙 2 "anchoredPosition 고정 픽셀값 사용 금지"

**현재 상태:**
```
anchorMin=(0.1,0)  anchorMax=(0.9,0.09)
pos=(0, 17)
```

**수정 후:**
```
anchorMin=(0.1,0)  anchorMax=(0.9,0.09)
pos=(0, 0)
```

---

### D-2. InfoBar > GoldIcon / PopIcon sizeDelta 제거 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

**현재 상태:**
```
sizeDelta=(0, 100)
LayoutElement: min=(100, -1), pref=(100, 100)
```

**수정 후:**
```
sizeDelta=(0, 0)
LayoutElement: min=(-1, -1), pref=(-1, -1), flexibleWidth=0, flexibleHeight=1
```

InfoBar HLG의 ctrl=(1,0), force=(1,0) 설정에서
아이콘은 flexibleWidth=0으로 비율 확장 없이 부모 높이를 따르도록 한다.

---

### D-3. InfoBar > GoldText / PopText sizeDelta 제거 (규칙 2)

**현재 상태:**
```
sizeDelta=(0, 100)
```

**수정 후:**
```
sizeDelta=(0, 0)
```

---

## 그룹 E: BorderOverlay 수정

### E-1. BorderOverlay ×3 sizeDelta/pos 정리 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

**현재 상태 (3개 공통):**
```
anchorMin=(0,0)  anchorMax=(1,1)  ignoreLayout=1
pos=(-284.66, 0) / (-1, 0) / (290, 0)  sizeDelta=(-569.32, 0)
sprite=NONE
```

**수정 후:**
```
anchorMin=(0,0)  anchorMax=(1,1)
pos=(0, 0)  sizeDelta=(0, 0)
```

anchor stretch (0,0)~(1,1)이면 pos=(0,0), sizeDelta=(0,0)으로 부모를 완전히 채운다.
sprite=NONE이므로 시각적 변화 없음.

---

## 구현 방식

모든 수정이 Inspector 값 변경이므로 각 그룹별로 1회성 Editor 스크립트를 작성한다.

| 스크립트 | 담당 그룹 | 메뉴 경로 |
|----------|----------|----------|
| `FixProductionPopupGroupA.cs` | 그룹 A (ProductionPanel) | `Hexiege/Fix/ProductionPopup/GroupA` |
| `FixProductionPopupGroupB.cs` | 그룹 B (ProgressBar) | `Hexiege/Fix/ProductionPopup/GroupB` |
| `FixProductionPopupGroupC.cs` | 그룹 C (QueueSlots) | `Hexiege/Fix/ProductionPopup/GroupC` |
| `FixProductionPopupGroupD.cs` | 그룹 D (InfoBar) | `Hexiege/Fix/ProductionPopup/GroupD` |
| `FixProductionPopupGroupE.cs` | 그룹 E (BorderOverlay) | `Hexiege/Fix/ProductionPopup/GroupE` |

### 실행 순서
1. 그룹 A → 씬 저장 → 패널 전체 크기 확인
2. 그룹 B → 씬 저장 → ProgressBar/Fill 시각 확인
3. 그룹 C → 씬 저장 → QueueSlots 레이아웃 확인
4. 그룹 D → 씬 저장 → InfoBar 아이콘/텍스트 크기 확인
5. 그룹 E → 씬 저장 → BorderOverlay 위치 확인
6. 에디터 Play Mode에서 배럭 클릭 → 생산 패널 전체 실기 확인

### 주의 사항
- 각 스크립트 실행 전 `Game.unity`가 열려 있어야 한다
- 실행 후 씬을 반드시 저장(Ctrl+S)해야 변경 사항이 유지된다
- 그룹 B (Fill 앵커)는 계산값이 근사치이므로 실기 확인 후 미세 조정 필요
- 그룹 C (SlotImage 앵커 0.1~0.9) 또한 실기 확인 후 비율 조정 가능

---

## 위험 요소

| 항목 | 위험 | 대응 |
|------|------|------|
| ProgressBar > Fill 비율값 | 스프라이트 실제 프레임 두께에 따라 어긋날 수 있음 | 실기 확인 후 anchorMin/Max 미세 조정 |
| QueueSlots Slot 크기 전환 | 160×160 → 비율 전환 시 슬롯이 너무 크거나 작을 수 있음 | 앵커 범위(25%~49%) 내에서 실기 확인 |
| InfoBar 아이콘 크기 | min/pref 제거 시 아이콘이 너무 작아질 수 있음 | flex 비율 재조정으로 대응 |

---

## 참고 파일

| 파일 | 용도 |
|------|------|
| `Assets/_Project/Docs/GameSystemRules.md` | UI 규칙 원본 |
| `Assets/_Project/Docs/_Tasks/2026-05-30/15_48_production-popup-rule-compliance/Research.md` | 위반 항목 실측 데이터 |
