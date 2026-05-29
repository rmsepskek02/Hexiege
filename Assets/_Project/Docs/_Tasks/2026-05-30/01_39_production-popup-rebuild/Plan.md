# Plan — ProductionPopup UI 재설계

## 작업 목적 (자연어 설명)

배럭(생산 건물) 클릭 시 나타나는 유닛 생산 패널을 비율 기반(반응형) 레이아웃으로 전환합니다.

**2026-05-30 1차 스크립트 실행 후 시각 문제 발견 → 설계 방향 수정:**
- 유닛 버튼 영역이 너무 커서 패널을 가리는 문제
- 큐 슬롯의 높이가 사라지는 문제
- 정보 바 크기/위치 문제

이를 반영하여 설계 방향을 아래와 같이 확정한다.

---

## 현재 씬 상태 (2차 작업 시작 기준)

| 항목 | 상태 | 비고 |
|------|------|------|
| HeaderText 앵커 | ✅ 완료 | anchorMin=(0.05,0.85), anchorMax=(0.85,0.97) |
| CancelButton 앵커 | ✅ 완료 | anchorMin=(0.883,0.852), anchorMax=(0.993,0.97) |
| UnitsButtons 영역 | ❌ 재작업 필요 | 버튼이 너무 크게 늘어남 |
| QueueSlots | ❌ 재작업 필요 | 높이 손실로 크기 이상 |
| InfoBar | ❌ 재작업 필요 | 위치·크기 모두 변경 필요 |
| ProgressBar / Fill | ✅ 건드리지 않음 | 앵커 정상 유지 중 |

---

## 수정 항목

### 1. HeaderText — ✅ 이미 완료, 재작업 불필요

---

### 2. CancelButton — ✅ 이미 완료, 재작업 불필요

---

### 3. UnitsButtons 영역 — 2행 3열 그리드 재구성

**근거**: GameSystemRules 공통 UI 규칙 Rule 2 (Layout Group 반응형 패턴)

**설계 방향**: BuildingPopup(BuildingPlacementUI)의 3×3 그리드와 동일한 VLG+HLG 중첩 구조를 사용하되, ProductionPopup은 **2행 3열** 구성이다.

- Row 1 (UnitButtons): 유닛 생산 버튼 3개
- Row 2 (Buttons): 액션 버튼 3개 (랠리, 업그레이드, 철거)

두 행은 균등한 높이로 분배된다 (별도 비율 없음, 1:1 그리드).

**UnitsButtons (VLG) 목표 설정**:
```
childControlWidth  = true
childControlHeight = true
childForceExpandWidth  = true
childForceExpandHeight = true
spacing = 적절한 간격
```

**UnitButtons / Buttons (각 HLG) 목표 설정**:
```
childControlWidth  = true
childControlHeight = true
childForceExpandWidth  = true
childForceExpandHeight = true
spacing = 적절한 간격
```

**LayoutElement 제거**: 기존에 추가된 flexibleHeight 비율(3/2)은 삭제 — 균등 그리드이므로 불필요.

**anchoredPosition / sizeDelta**: 두 행 모두 (0,0) 유지.

---

### 4. QueueSlots — 정사각형 슬롯, 좌우 균등 중앙 배치

**근거**: GameSystemRules 공통 UI 규칙 Rule 2 (앵커 기반 배치 원칙)

**설계 방향**:
- 슬롯 3개는 정사각형을 유지하면서 QueueSlots 영역 내 **좌우 균등하게 중앙 배치**
- HLG가 가로 분배하되 슬롯이 영역을 꽉 채우지 않고 적절한 크기로 중앙에 위치

**QueueSlots HLG 목표 설정**:
```
childControlWidth  = false   ← 슬롯이 자체 크기를 유지하도록
childForceExpandWidth = false ← 꽉 채우지 않음
childAlignment = MiddleCenter ← 가운데 중앙 정렬
spacing = 적절한 간격
```

**각 슬롯 (Slot1/2/3) 목표 설정**:
```
sizeDelta = (N, N)  ← 정사각형 (N 값은 QueueSlots 높이의 약 70~80% 기준으로 결정)
anchoredPosition = (0, 0)
```

> QueueSlots 영역 높이 = ProductionPanel의 33% (anchor 0.34~0.67). 실기 확인 후 N 값 조정.

---

### 5. InfoBar — 패널 프레임 하단 중앙, 소형 배치

**근거**: GameSystemRules 공통 UI 규칙 Rule 2 (앵커 기반 배치 원칙)

**설계 방향**:
- 패널 외곽 프레임 이미지의 **하단 경계선 위, 수평 중앙**에 배치
- 높이 = 패널 프레임 하단 테두리 두께 수준의 소형 크기
- 골드 아이콘 + 골드 수치 + 인구 아이콘 + 인구 수치가 한 줄로 표시

**InfoBar (RT + HLG) 목표 설정**:
```
anchorMin = (0.1, 0)  ← X축 10%~90% 중앙 배치 (실기 조정 필요)
anchorMax = (0.9, 0.1)
anchoredPosition = (0, 0)
sizeDelta = (0, 0)

HLG:
  childControlWidth = true
  childForceExpandWidth = true
  childAlignment = MiddleCenter
```

**InfoBar 자식 요소 설정**:
```
GoldIcon, PopIcon (아이콘):
  sizeDelta = (0, 0)
  LayoutElement: preferredWidth = InfoBar 높이와 동일 (정사각형)
                 flexibleWidth = 0

GoldText, PopText (텍스트):
  sizeDelta = (0, 0)
  LayoutElement: flexibleWidth = 1
```

> anchorMax.y = 0.1은 초기 추정값이다. 패널 프레임 이미지의 실제 하단 테두리 두께에 따라
> 실기 확인 후 조정한다.

---

## 구현 방식

### 2회성 Editor 스크립트 (1차 수정분 위에 덮어쓰기)

**파일**: `Assets/Editor/FixProductionPopup.cs`
**메뉴**: `Hexiege/Setup/ProductionPopup 2차 수정`

1차 스크립트(`RebuildProductionPopup.cs`)로 HeaderText/CancelButton은 이미 완료.
이 스크립트는 **나머지 3개 영역만** 수정한다.

스크립트에서 수행하는 작업:
1. UnitsButtons VLG / UnitButtons HLG / Buttons HLG 설정 재조정 + 기존 LayoutElement 삭제
2. QueueSlots HLG alignment/childControl 변경 + 각 Slot sizeDelta 정사각형으로 복원
3. InfoBar RT 앵커를 프레임 하단 중앙으로 재설정 + 자식 요소 LayoutElement 조정

---

## Inspector 확인 목록

2차 스크립트 실행 후 확인:

| 필드 | 확인 내용 |
|------|-----------|
| `_unitButtons` (3개) | UnitButtons 행의 유닛 버튼 — null 아닌지 |
| `_queueSlotImages` (3개) | Slot1/2/3 Image — null 아닌지 |
| `_goldText` | InfoBar > GoldText — null 아닌지 |
| `_populationText` | InfoBar > PopText — null 아닌지 |

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| QueueSlots 슬롯 크기 N 값 | 정사각형 크기는 QueueSlots 높이에 따라 조정 필요 | 실기 확인 후 sizeDelta 값 조정 |
| InfoBar anchorMax.y 값 | 패널 프레임 테두리 두께가 불명확 | 실기 확인 후 0.1 → 조정 |
| LayoutElement 삭제 시 참조 오류 | 1차 스크립트가 추가한 LayoutElement 삭제 필요 | DestroyImmediate(le) + Undo 등록 |
| 업그레이드 버튼 숨김 | CanvasGroup.alpha=0인 버튼이 Grid 공간을 차지하므로 레이아웃 유지됨 | 확인 필요 |

---

## 작업 순서

1. `Assets/Editor/FixProductionPopup.cs` 작성 (game-programmer 에이전트)
2. Unity에서 `Hexiege/Setup/ProductionPopup 2차 수정` 실행 (사용자)
3. 실기 확인 후 슬롯 크기(N) 및 InfoBar 앵커 미세 조정
4. Inspector 필드 연결 확인

---

## 참고 파일

| 파일 | 용도 |
|------|------|
| `Assets/_Project/Docs/GameSystemRules.md` | UI 규칙 원본 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs` | 3×3 그리드 패턴 참고 |
| `Assets/_Project/Docs/_Tasks/2026-05-29/building-placement-ui-rebuild/Plan.md` | BuildingPopup 그리드 구현 참고 |
