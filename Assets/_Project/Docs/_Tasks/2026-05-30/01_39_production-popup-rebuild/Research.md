# Research — ProductionPopup UI 재설계

## 작업 목적 (자연어 설명)

배럭(생산 건물)을 클릭하면 나타나는 유닛 생산 패널(ProductionPopup)의 레이아웃이
고정 픽셀 값으로 하드코딩되어 있습니다.

이 때문에 기기 화면 크기나 해상도가 달라지면 버튼, 텍스트, 슬롯 등의 위치와 크기가
어긋나거나 잘리는 문제가 생깁니다.

이전에 완료한 BuildingPopup(건물 배치 패널)과 BuildingActionPanel(건물 액션 패널)과
동일한 방식으로, 고정 픽셀 값을 제거하고 모든 요소를 비율 기반(앵커)으로 전환합니다.
이를 통해 어떤 화면 크기에서도 깔끔하게 표시되는 반응형 UI로 만드는 것이 목표입니다.

---

## 분석 대상

- **씬**: `Assets/_Project/Scenes/Game.unity`
- **대상 오브젝트**: `ProductionPopup > ProductionPanel`
- **관련 스크립트**: `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

---

## 현재 계층 구조 및 위반 목록

```
ProductionPopup (래퍼)       ✅ anchor(0,0)~(1,1)
  └── ProductionPanel        ✅ anchor(0,0)~(1,0.4)
        ├── HeaderText        ❌ Y축 단일점 앵커(0.5~0.5), anchoredPosition=(-25,210), sizeDelta=(-150,50)
        ├── CancelButton      ⚠️ anchorMin=(0.87,0.78), anchorMax=(1,1) — 다른 패널과 불일치
        ├── UnitsButtons(VLG) ⚠️ childControlHeight=false
        │     ├── UnitButtons ❌ sizeDelta=(1070,0) 고정 픽셀 (유닛 버튼 행)
        │     └── Buttons     ❌ sizeDelta=(1070,100) 고정 픽셀 (액션 버튼 행: 랠리/업그레이드/철거)
        ├── QueueSlots (HLG)  ❌ childControlWidth/Height=false, spacing=50 고정
        │     ├── Slot1       ❌ anchoredPosition=(400,-126.72), sizeDelta=(90,80)
        │     ├── Slot2       ❌ anchoredPosition=(540,-126.72), sizeDelta=(90,80)
        │     └── Slot3       ❌ anchoredPosition=(680,-126.72), sizeDelta=(90,80)
        ├── ProgressBar       ✅ anchor(0,0.17)~(1,0.34)  ← 이미 OK, 수정 대상 아님
        │     └── Fill        ✅ anchor(0,0)~(1,1)
        └── InfoBar (HLG)     ❌ childControlWidth/Height=false
              ├── GoldIcon    ❌ anchoredPosition=(410,-65.28), sizeDelta=(60,60)
              ├── GoldText    ❌ anchoredPosition=(490,-65.28), sizeDelta=(100,20)
              ├── PopIcon     ❌ anchoredPosition=(570,-65.28), sizeDelta=(60,60)
              └── PopText     ❌ anchoredPosition=(650,-65.28), sizeDelta=(100,20)
```

---

## 위반 항목별 상세 분석

### 1. HeaderText — Y축 단일점 앵커

- **현재**: anchorMin/anchorMax의 Y가 모두 0.5 (화면 중앙 단일 기준선), anchoredPosition=(-25,210)으로 픽셀 오프셋으로 위치 결정
- **문제**: 화면 높이가 달라지면 픽셀 오프셋으로 인해 위치가 틀어짐
- **GameSystemRules Rule 2 위반**: "sizeDelta, offsetMin, offsetMax 등 고정 픽셀값 사용 금지"

### 2. CancelButton — 위치 불일치

- **현재**: anchorMin=(0.87,0.78), anchorMax=(1,1)
- **다른 패널 기준**: anchorMin=(0.883,0.852), anchorMax=(0.993,0.97)
- **문제**: BuildingPopup, BuildingActionPanel과 위치가 달라 패널마다 닫기 버튼 위치가 다름

### 3. UnitsButtons VLG — childControlHeight=false

- **현재**: 자식 행들(UnitButtons, Buttons)의 높이를 직접 제어하지 않음
- **문제**: UnitButtons는 sizeDelta.y=0 (높이 미정), Buttons는 sizeDelta.y=100 고정 픽셀
- **연쇄 영향**: childControlWidth도 false로 추정 — sizeDelta.x=1070 고정 픽셀이 사용 중

### 4. UnitButtons / Buttons (액션행) — 고정 픽셀 sizeDelta

- **현재**: sizeDelta=(1070,0), sizeDelta=(1070,100) — 1080px 기준 디자인에 고정
- **문제**: 가로 해상도 비율이 달라지면 버튼 행이 잘리거나 빈 공간이 생김

### 5. QueueSlots HLG — childControl 비활성 + Slot 고정 위치

- **현재**: childControlWidth/Height=false, spacing=50 고정
  - 각 Slot의 anchoredPosition이 고정 픽셀 좌표로 지정됨
  - Slot sizeDelta=(90,80) 고정 픽셀
- **문제**: HLG가 자식 크기를 제어하지 못하므로 반응형 분배 불가

### 6. InfoBar HLG — childControl 비활성 + 아이콘/텍스트 고정

- **현재**: childControlWidth/Height=false
  - GoldIcon, PopIcon: sizeDelta=(60,60) 고정
  - GoldText, PopText: sizeDelta=(100,20) 고정
  - 모두 anchoredPosition 고정 픽셀 값
- **문제**: InfoBar 너비가 달라져도 아이콘/텍스트 위치가 고정

---

## 수정 대상에서 제외되는 요소

| 오브젝트 | 이유 |
|----------|------|
| ProductionPopup (래퍼) | anchor(0,0)~(1,1) 이미 OK |
| ProductionPanel | anchor(0,0)~(1,0.4) 이미 OK |
| ProgressBar / Fill | 완전한 앵커 기반 이미 OK |

---

## 관련 코드 파일

| 파일 | 용도 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | Inspector 필드 참조 (큐 슬롯, 골드/인구 텍스트, 버튼 등) |
| `Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs` | 이전 작업 참고 패턴 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs` | CanvasGroup 슬롯 제어 패턴 참고 |
| `Assets/_Project/Docs/GameSystemRules.md` | UI 규칙 원본 (Rule 1~10) |

---

## ProductionPanelUI Inspector 필드 요약 (씬 계층 연결 대상)

씬 재구성 후 연결이 유지되어야 하는 주요 필드:

| 필드명 | 연결 대상 오브젝트 |
|--------|------------------|
| `_unitButtons` (List) | UnitButtons 행의 유닛 버튼 3개 |
| `_unitButtonPortraits` (List) | 유닛 버튼 초상화 Image |
| `_unitCostTexts` (List) | 유닛 버튼 비용 텍스트 TMP |
| `_unitAutoIndicators` (List) | 자동 생산 인디케이터 GO |
| `_unitBorderOverlays` (List) | 자동 생산 테두리 오버레이 Image |
| `_unitLockIndicators` (List) | 잠금 오버레이 GO |
| `_unitButtonGroups` (List) | 유닛 버튼 CanvasGroup |
| `_queueSlotImages` (Array) | QueueSlots의 Slot1/2/3 Image |
| `_progressFill` | ProgressBar > Fill |
| `_goldText` | InfoBar > GoldText |
| `_populationText` | InfoBar > PopText |
| `_rallyPointButton` | Buttons 행의 랠리 버튼 |
| `_upgradeButton` | Buttons 행의 업그레이드 버튼 |
| `_upgradeButtonGroup` | 업그레이드 버튼 CanvasGroup |
| `_upgradeCostText` | 업그레이드 비용 텍스트 TMP |
| `_upgradeIconImage` | 업그레이드 버튼 아이콘 Image |

> 위 필드들은 Unity에서 씬 오브젝트와 직렬화 참조로 연결되어 있으므로,
> 오브젝트를 부모 이동(Reparent) 하는 경우 참조가 유지되나, 실행 후 Inspector에서 확인 필요.
> 오브젝트를 삭제/재생성하는 경우 반드시 재연결이 필요하다.

---

## 참고: 이전 작업 패턴 (동일한 방식 적용 예정)

### BuildingActionPanel 재설계 (완료)
- 1회성 Editor 스크립트(`RebuildBuildingActionPanel.cs`) 작성 → Unity 메뉴에서 실행
- RectTransform 수정: anchoredPosition=(0,0), sizeDelta=(0,0)로 초기화
- Layout Group 수정: childControl + childForceExpand = true
- CanvasGroup 패턴: 빈 슬롯은 alpha=0, blocksRaycasts=false로 숨김

### 핵심 규칙
- 앵커 변경 후 반드시: `rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;`
- 빈 슬롯 숨김: SetActive(false) 금지 → CanvasGroup.alpha=0 사용
