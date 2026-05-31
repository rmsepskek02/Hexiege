# Research — BuildingPopup / BuildingActionPanel / ProductionPopup 버튼 크기 불일치

## 작업 목적 (자연어 설명)

BuildingPopup(건물 배치 패널), BuildingActionPanel(비생산 건물 액션 패널), ProductionPopup(생산 건물 패널)
세 곳에서 버튼들의 크기가 조금씩 다르게 보이는 문제가 있다.

사용자 보고에 따르면:
- BuildingPopup: 1행과 2행의 버튼 크기가 다르게 보임
- ProductionPopup: 6개 버튼(Row0 유닛 3개 + Row1 액션 3개)이 각각 조금씩 달라 보임
- 패널 높이를 키운 시점(ProductionPopup 포함 전체 패널 AnchorMax.y 증가)부터 현상 발생

---

## 분석 대상

- **씬**: `Assets/_Project/Scenes/Game.unity`
- **대상 오브젝트**:
  - `BuildingPopup > BuildingPanel > GridContainer`
  - `BuildingActionPanel > BuildingPanel > GridContainer`
  - `ProductionPopup > ProductionPanel > GridContainer`

---

## 씬 파일 파싱 결과 (저장된 상태 기준)

### 패널 최상위 구조 비교

| 항목 | BuildingPopup > BuildingPanel | BuildingActionPanel > BuildingPanel | ProductionPopup > ProductionPanel |
|------|------|------|------|
| AnchorMin | (0, 0) | (0, 0) | (0, 0) |
| AnchorMax | **(1, 0.5)** | **(1, 0.5)** | **(1, 0.5)** |
| SizeDelta | (0, 0) | (0, 0) | (0, 0) |
| Pivot | (0.5, 0) | (0.5, 0) | (0.5, 0) |
| 실제 높이 | 0.5 × 1920 = **960px** | 0.5 × 1920 = **960px** | 0.5 × 1920 = **960px** |

→ **세 패널 모두 동일한 높이** (960px)

---

### GridContainer 구조 비교

| 항목 | BuildingPopup | BuildingActionPanel | ProductionPopup |
|------|------|------|------|
| AnchorMin | (0.08, 0.123) | (0.08, 0.123) | (0.08, 0.123) |
| AnchorMax | (0.92, 0.864) | (0.92, 0.864) | (0.92, 0.864) |
| SizeDelta | (0, 0) | (0, 0) | (0, 0) |
| 실제 높이 | 0.741 × 960 = **711px** | 0.741 × 960 = **711px** | 0.741 × 960 = **711px** |
| VLG Padding | L20 R20 T20 B20 | L20 R20 T20 B20 | L20 R20 T20 B20 |
| VLG Spacing | 8 | 8 | 8 |
| childControlHeight | true | true | true |
| childForceExpandHeight | true | true | true |

→ **GridContainer 크기와 VLG 설정 모두 동일**

---

### Row 구조 비교

| 항목 | BuildingPopup | BuildingActionPanel | ProductionPopup |
|------|------|------|------|
| Row 수 | 3개 (Row0/1/2 모두 IsActive=1) | 3개 (Row0/1/2 모두 IsActive=1) | 3개 (Row0/1/2 모두 IsActive=1) |
| Row SizeDelta (캐시) | **(867.2, 218.45)** | **(867.2, 218.45)** | **(867.2, 218.45)** |
| Row HLG Padding | L0 R0 T0 B0 | L0 R0 T0 B0 | L0 R0 T0 B0 |
| Row HLG Spacing | 8 | 8 | 8 |
| Row HLG forceExpand | true | true | true |
| LayoutElement 여부 | **없음** | **없음** | **없음** |

→ **Row 높이 이론값: (711 - 40 - 16) / 3 = 218.45px 로 동일해야 함**

---

### Slot(버튼) 구조 비교

| 항목 | BuildingPopup Slot | BuildingActionPanel Slot | ProductionPopup 일반 Slot |
|------|------|------|------|
| HLG Padding | **L60 R60 T20 B20** | **L60 R60 T20 B20** | **L60 R60 T20 B20** |
| HLG Spacing | 6 | 6 | 6 |
| Slot SizeDelta (캐시) | (283.73, 218.45) | (283.73, 218.45) | (283.73, 218.45) |
| IconImage FlexibleWidth | 6 | 6 | 6 |
| IconImage PreserveAspect | true | true | true |

---

### ProductionPopup 특수 슬롯 — Rallypoint

ProductionPopup Row1에만 존재하는 Rallypoint 버튼이 다른 슬롯과 **다른 HLG 패딩**을 사용한다.

| 항목 | 일반 슬롯 (유닛/업그레이드/철거) | Rallypoint 슬롯 |
|------|------|------|
| HLG Padding | L60 R60 T20 B20 | **L125 R125 T0 B0** |
| Slot SizeDelta (캐시) | (283.73, 218.45) | **(277.14, 218.45)** |
| 아이콘 가용 너비 | 283.73 − 120 = **163.73px** | 277.14 − 250 = **27.14px** |
| 실제 표시 아이콘 크기 | 163.73 × 0.6 = **약 98px** | **약 27px** |

→ Rallypoint 아이콘이 다른 버튼 아이콘의 **약 1/4 수준**으로 표시됨

---

## 한계 — 정적 분석으로 설명되지 않는 부분

씬 파일 기준으로 세 패널의 모든 구조값이 동일하므로, **저장된 데이터만으로는 Row0과 Row1의 크기 차이(첫 번째 이미지)를 설명할 수 없다**.

사용자 보고 내용:
- BuildingPopup 1행 버튼 > 2행 버튼 (크기 차이 시각적으로 확인됨)
- 패널 높이를 키우고 나서 발생

가설: 패널 높이 변경 시 GridContainer 내부 앵커값이 서로 다른 방식으로 재계산되거나, 현재 Unity 에디터의 미저장 상태가 씬 파일과 다를 가능성이 있다. **MCP로 실제 Inspector 값을 확인해야 정확한 원인 파악 가능.**

---

## 파악된 확실한 문제

### 문제 1 — ProductionPopup Rallypoint 패딩 과다 (코드 확인 완료)

- **위치**: `Assets/_Project/Scenes/Game.unity` > ProductionPopup > ProductionPanel > GridContainer > Row1 > Rallypoint
- **현재 HLG Padding**: L125 R125 T0 B0
- **다른 슬롯 Padding**: L60 R60 T20 B20
- **영향**: Rallypoint 아이콘이 다른 버튼 대비 약 1/4 크기로 표시됨

### 문제 2 — Row0 vs Row1 크기 차이 (원인 미확정)

- **증상**: BuildingPopup에서 Row0(1행) 버튼이 Row1(2행)보다 크게 보임
- **씬 파일 데이터 상**: 동일한 구조여야 함
- **원인 미파악**: Runtime 레이아웃 계산 또는 미저장 에디터 상태 차이로 추정
- **다음 단계**: MCP Inspector 실시간 확인 필요

---

## 관련 파일

| 파일 | 용도 |
|------|------|
| `Assets/_Project/Scenes/Game.unity` | 실제 씬 데이터 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs` | BuildingPopup 스크립트 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs` | BuildingActionPanel 스크립트 |
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | ProductionPopup 스크립트 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs` | 3개 패널 공통 베이스 |
| `Assets/_Project/Docs/GameSystemRules.md` | UI 규칙 원본 (Rule 1~10) |

---

## 관련 이전 작업

| 작업 | 경로 |
|------|------|
| ProductionPopup UI 재설계 (2026-05-30) | `Assets/_Project/Docs/_Tasks/2026-05-30/01_39_production-popup-rebuild/` |
| ProductionPopup 규칙 준수 점검 (2026-05-30) | `Assets/_Project/Docs/_Tasks/2026-05-30/15_48_production-popup-rule-compliance/` |
