# Plan — ProductionPopup UI 재설계

## 작업 목적 (자연어 설명)

배럭(생산 건물) 클릭 시 나타나는 유닛 생산 패널을 비율 기반(반응형) 레이아웃으로 전환합니다.

**2026-05-30 1차 스크립트 실행 후 시각 문제 발견 → 설계 방향 수정:**
- 유닛 버튼 영역이 너무 커서 패널을 가리는 문제
- 큐 슬롯의 높이가 사라지는 문제
- 정보 바 크기/위치 문제

이를 반영하여 설계 방향을 아래와 같이 확정한다.

---

## 현재 씬 상태 (최신 기준)

스크립트 누적 실행 결과:

| 항목 | 상태 | 비고 |
|------|------|------|
| HeaderText 앵커 | ✅ 완료 | anchorMin=(0.05,0.85), anchorMax=(0.85,0.97) |
| CancelButton 앵커 | ✅ 완료 | anchorMin=(0.883,0.852), anchorMax=(0.993,0.97) |
| ProgressBar Y | ✅ 완료 | anchorMin.y=0.17, anchorMax.y=0.25 |
| QueueSlots Y | ✅ 완료 | anchorMin.y=0.25, anchorMax.y=0.49 |
| UnitsButtons Y | ✅ 완료 | anchorMin.y=0.49, anchorMax.y=0.97 |
| UnitsButtons X + VLG 패딩 | ✅ 완료 | anchorMin.x=0.08, anchorMax.x=0.92, Padding=20, Spacing=8 |
| UnitsButtons GridContainer 구조 | ✅ 완료 | ApplyGridContainer.cs 실행 완료 |
| 유닛 버튼 내부 구조 (BuildingPopup 동일화) | ❌ 미완료 | 이번 작업 대상 |
| QueueSlots 슬롯 정렬 | ❌ 미완료 | 왼쪽 치우침 문제 잔존 |
| InfoBar 위치/크기 | ❌ 미완료 | 별도 작업 필요 |

---

## 수정 항목

### 1. HeaderText — ✅ 완료

---

### 2. CancelButton — ✅ 완료

---

### 3. UnitsButtons 영역 — BuildingPopup과 동일한 계층 구조 적용

**근거**: GameSystemRules 공통 UI 규칙 Rule 2 (Layout Group 반응형 패턴)

**설계 방향**: BuildingPopup의 `BuildingPanel → GridContainer → VLG → Row × N` 구조를 그대로 따른다.
UnitsButtons가 BuildingPanel 역할(외부 컨테이너), GridContainer(신규 생성)가 실제 그리드를 담당한다.

**목표 계층 구조**:
```
UnitsButtons (단순 컨테이너, VLG 제거)
  └── GridContainer (신규 생성)
        anchor: (0.08, 0.123) ~ (0.92, 0.864)  ← BuildingPopup GridContainer와 동일
        anchoredPosition: (0, 0), sizeDelta: (0, 0)
        VLG:
          padding (Left/Right/Top/Bottom) = 20   ← BuildingPopup GridContainer VLG와 동일
          spacing = 8
          childAlignment = MiddleCenter
          childControlWidth/Height = true
          childForceExpandWidth/Height = true
        ├── UnitButtons (HLG — 유닛 버튼 3개, GridContainer 하위로 이동)
        └── Buttons (HLG — 액션 버튼 3개, GridContainer 하위로 이동)
```

**BuildingPopup과 비교**:

| | BuildingPopup | ProductionPopup |
|---|---|---|
| 외부 컨테이너 | BuildingPanel | UnitsButtons |
| 그리드 컨테이너 | GridContainer | GridContainer (신규) |
| GridContainer anchor | (0.08, 0.123)~(0.92, 0.864) | 동일 |
| VLG padding | 20 (상하좌우) | 동일 |
| VLG spacing | 8 | 동일 |
| 행 수 | 3행 | 2행 |

**작업 내용**:
1. UnitsButtons에서 VerticalLayoutGroup 제거
2. UnitsButtons 자식으로 GridContainer 오브젝트 신규 생성
3. GridContainer에 위 RectTransform 수치 적용
4. GridContainer에 VLG 추가 (BuildingPopup과 동일 값)
5. UnitButtons, Buttons를 GridContainer 하위로 Reparent

**⚠️ Inspector 재연결 필요**: UnitButtons, Buttons를 Reparent해도 ProductionPanelUI의 SerializedField 참조는 유지되나, 실행 후 반드시 Inspector에서 null 여부 확인.

**UnitButtons / Buttons (각 HLG) 현재 설정** (변경 없음 유지):
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

### 현재까지 실행된 스크립트

| 스크립트 | 적용 내용 |
|----------|-----------|
| `RebuildProductionPopup.cs` | HeaderText, CancelButton 앵커 수정 |
| `FixProductionPopup.cs` | UnitsButtons 그리드 설정, QueueSlots, InfoBar 1차 조정 |
| `FixProductionPopupGrid.cs` | UnitsButtons 앵커 x(0.08~0.92), VLG Padding=20, Spacing=8 |
| `FixProductionPopupRatio.cs` | ProgressBar/QueueSlots/UnitsButtons Y앵커 6:3:1 비율 조정 |
| `ApplyGridContainer.cs` | UnitsButtons에 GridContainer 생성, UnitButtons/Buttons 이동 |

### 4. 유닛 버튼 내부 구조 — BuildingPopup Slot과 동일하게

**근거**: GameSystemRules 공통 UI 규칙 Rule 2 (Layout Group 반응형 패턴)

**설계 방향**: GridContainer > UnitButtons 행의 버튼 3개(Button1/2/3)를 BuildingPopup의 Slot과 동일한 내부 구조로 변환한다.

**BuildingPopup Slot 구조 (실측값 기준)**:
```
Slot (Button + Image + HLG + CanvasGroup)
  HLG: Padding L=60, R=60, T=20, B=20, Spacing=6
       childAlignment=MiddleCenter
       childControlWidth/Height=true, childForceExpandWidth/Height=true
  ├── IconImage (Image + LayoutElement: flexibleWidth=6, flexibleHeight=1)
  └── CostContainer (VLG: spacing=4, childControlW/H=true, childForceExpandW=true, H=false)
        ├── GoldIcon (Image + LayoutElement: minWidth=44, minHeight=44,
        │                                    preferredWidth=44, preferredHeight=44)
        └── CostText (TMP + LayoutElement: preferredWidth=400, preferredHeight=22)
```

**ProductionPopup Button (현재 → 목표)**:
```
Button1/2/3 (현재: Button + Image + CanvasGroup, 자식들이 앵커 기반 직접 배치)
  →
Button1/2/3 (목표: Button + Image + HLG + CanvasGroup)
  HLG: Padding L=60, R=60, T=20, B=20, Spacing=6  ← BuildingPopup과 동일
       childAlignment=MiddleCenter
       childControlWidth/Height=true, childForceExpandWidth/Height=true
  ├── UnitImage (Image, 기존 유지 + LayoutElement 추가: flexibleWidth=6, flexibleHeight=1)
  ├── CostContainer (신규 VLG, BuildingPopup CostContainer와 동일)
  │     ├── GoldIcon (Button 직속 → CostContainer 하위로 이동, LayoutElement 추가)
  │     └── GoldText (Button 직속 → CostContainer 하위로 이동, LayoutElement 추가)
  └── BorderOverlay (기존 유지, LayoutElement: ignoreLayout=true 추가 → HLG 배치에서 제외)
```

**파일**: `Assets/Editor/ApplyButtonStructure.cs`
**메뉴**: `Hexiege/Setup/ProductionPopup 버튼 구조 적용`

---

## Inspector 확인 목록

스크립트 실행 후 ProductionPanelUI 컴포넌트 확인:

| 필드 | 확인 내용 |
|------|-----------|
| `_unitButtons` (3개) | Button1/2/3 — null 아닌지 |
| `_unitButtonPortraits` (3개) | UnitImage의 Image 컴포넌트 — null 아닌지 |
| `_unitCostTexts` (3개) | GoldText TMP — null 아닌지 (CostContainer 이동 후) |
| `_unitBorderOverlays` (3개) | BorderOverlay Image — null 아닌지 |
| `_rallyPointButton` | Buttons 행의 랠리 버튼 — null 아닌지 |
| `_upgradeButton` | Buttons 행의 업그레이드 버튼 — null 아닌지 |

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| GoldText/GoldIcon Reparent 후 참조 | CostContainer로 이동 시 _unitCostTexts 참조 유지 여부 | Inspector에서 null 체크 |
| BorderOverlay HLG 간섭 | ignoreLayout=true 없으면 HLG가 BorderOverlay도 레이아웃에 포함 | LayoutElement.ignoreLayout=true 필수 |
| UnitImage 앵커 → LayoutElement 전환 | 기존 anchor(0,0~1,1)에서 LayoutElement 기반으로 변환 | HLG childControl=true이면 앵커 무시됨 |

---

## 작업 순서

1. `Assets/Editor/ApplyButtonStructure.cs` 작성 (game-programmer 에이전트)
2. Unity에서 `Hexiege/Setup/ProductionPopup 버튼 구조 적용` 실행
3. Inspector에서 ProductionPanelUI 필드 null 체크
4. 에디터 플레이 모드에서 배럭 클릭 → 생산 패널 확인

---

## 참고 파일

| 파일 | 용도 |
|------|------|
| `Assets/_Project/Docs/GameSystemRules.md` | UI 규칙 원본 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs` | 3×3 그리드 패턴 참고 |
| `Assets/_Project/Docs/_Tasks/2026-05-29/building-placement-ui-rebuild/Plan.md` | BuildingPopup 그리드 구현 참고 |
