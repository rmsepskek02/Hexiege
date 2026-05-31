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

Rallypoint의 자식 IconImage에 `IgnoreLayout: 1` + 앵커 배치가 적용되어 있어, HLG 패딩이 아이콘 크기에 전혀 영향을 미치지 않는다.

**Rallypoint 슬롯 구조 (씬 파일 직접 확인):**
```
Rallypoint (HLG: L125 R125 T0 B0)
  └── IconImage
        LayoutElement: IgnoreLayout = true   ← HLG 레이아웃 계산 제외
        AnchorMin = (0, 0), AnchorMax = (1, 1)
        SizeDelta = (-80, -80)               ← 슬롯 크기 기준 상하좌우 40px 여백
        PreserveAspect = true
```

**IgnoreLayout=true의 의미**: HLG의 L125 R125 패딩이 아이콘에 적용되지 않고, 대신 슬롯 전체 크기에서 상하좌우 40px을 뺀 영역으로 배치된다.

| 항목 | 일반 슬롯 (유닛 버튼) | Rallypoint |
|------|------|------|
| 아이콘 크기 결정 방식 | HLG flex 분배 (L60 R60 제외 후 60%) | 앵커 (슬롯 − 80px) |
| HLG Padding | L60 R60 T20 B20 | L125 R125 T0 B0 (아이콘에 무효) |
| 아이콘 너비 | (283−120) × 0.6 = **약 98px** | 277−80 = **약 197px** |
| 아이콘 높이 | 218−40 = **178px** | 218−80 = **138px** |

→ Rallypoint 아이콘이 일반 유닛 버튼 아이콘보다 **가로 기준 약 2배** 크게 표시됨. 이미지에서 Rallypoint가 더 크게 보이는 것과 일치.

---

## 한계 — 정적 분석으로 설명되지 않는 부분

씬 파일 기준으로 세 패널의 모든 구조값이 동일하므로, **저장된 데이터만으로는 Row0과 Row1의 크기 차이(첫 번째 이미지)를 설명할 수 없다**.

사용자 보고 내용:
- BuildingPopup 1행 버튼 > 2행 버튼 (크기 차이 시각적으로 확인됨)
- 패널 높이를 키우고 나서 발생

가설: 패널 높이 변경 시 GridContainer 내부 앵커값이 서로 다른 방식으로 재계산되거나, 현재 Unity 에디터의 미저장 상태가 씬 파일과 다를 가능성이 있다. **MCP로 실제 Inspector 값을 확인해야 정확한 원인 파악 가능.**

---

## 파악된 확실한 문제

### 문제 1 — ProductionPopup Rallypoint 아이콘이 다른 버튼보다 약 2배 크게 표시됨

- **위치**: ProductionPopup > Row1 > Rallypoint > IconImage
- **원인**: IconImage에 `IgnoreLayout=true` + `Anchor(0,0)~(1,1) SizeDelta=(-80,-80)` 적용
  → HLG 패딩 무시하고 슬롯 전체 크기 기준 배치
- **결과**: Rallypoint 아이콘 ≈ **197px × 138px** vs 유닛 버튼 아이콘 ≈ **98px × 178px**
- **의도된 설계**: Rallypoint 아이콘을 크게 보이도록 의도적으로 선택한 방식. 버그 아님.

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
