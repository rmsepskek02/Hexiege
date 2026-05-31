# Plan — BuildingPopup / BuildingActionPanel / ProductionPopup 버튼 크기 불일치 수정

## 작업 목적 (자연어 설명)

세 패널의 버튼(슬롯)이 행마다 크기가 달라 보이는 문제를 수정한다.
런타임 로그 분석으로 원인이 확정됐으며, Unity Inspector에서 LayoutElement 컴포넌트를 추가하는 것으로 해결한다.
기존 코드 수정은 없고, 씬 파일(Game.unity)만 변경된다.

---

## 원인 요약 (Research.md 결론)

슬롯 내부 아이콘 Image의 스프라이트 native size가 VLG의 Row 높이 배분에 영향을 준다.

Unity 레이아웃 배분 3단계:
1. minHeight 배분
2. preferredHeight 배분 ← 여기서 스프라이트 크기 차이로 인해 Row별 불균등 발생
3. flexibleHeight 배분 ← childForceExpandHeight=True 효과 (이미 늦음)

`childForceExpandHeight=True`만으로는 부족하다. preferred 단계에서 이미 불균등해진 상태에 작동하기 때문.

---

## 수정 내용

### 수정 1 — Row에 LayoutElement 추가 (높이 균등화)

**대상:**
- `BuildingPopup > BuildingPanel > GridContainer > Row0 / Row1 / Row2`
- `BuildingActionPanel > BuildingPanel > GridContainer > Row0 / Row1 / Row2`
- `ProductionPopup > ProductionPanel > GridContainer > Row0 / Row1 / Row2`

**설정값:**
```
LayoutElement:
  preferredHeight = 0    ← 스프라이트 preferred height 차단
  flexibleHeight  = 1    ← VLG가 남은 공간을 균등 분배
  (나머지 항목 = -1, 비활성화)
```

**근거:** GameSystemRules.md 공통 UI 규칙 2 — "Control Child Size + Child Force Expand를 활성화하면 가용 공간을 자동으로 균등 분배한다." LayoutElement(preferredHeight=0)는 이 균등 분배가 스프라이트 크기에 방해받지 않도록 보장하는 것이다.

**예상 결과:** `(711.36 - 40 - 16) / 3 = 218.45px` — 세 Row 모두 동일 높이

---

### 수정 2 — Slot에 LayoutElement 추가 (너비 균등화)

**대상:** 위 세 패널 각각의 Row0/Row1/Row2 하위 **모든 Slot** (총 9개 × 3패널 = 27개)

**설정값:**
```
LayoutElement:
  preferredWidth = 0    ← 스프라이트 preferred width 차단
  flexibleWidth  = 1    ← HLG가 남은 공간을 균등 분배
  (나머지 항목 = -1, 비활성화)
```

**근거:** GameSystemRules.md 공통 UI 규칙 2 동일. Row 내 슬롯 너비도 동일 원인으로 불균등하므로 같은 방식으로 수정.

**예상 결과:** `(867.20 - 16) / 3 = 283.73px` — 모든 Slot 동일 너비 (BuildingActionPanel과 동일)

---

## 구현 방법

Inspector 수동 작업 대신 **1회성 Editor 스크립트**로 자동 처리한다 (WORKFLOW.md [5-2]).

- 파일: `Assets/Editor/FixPanelRowLayout.cs`
- 메뉴: `Hexiege/Fix Panel Row Layout`
- 동작: Row와 Slot에 LayoutElement 추가 → 값 설정 → 씬 저장

---

## 수정 파일 목록

| 파일 | 변경 내용 |
|------|-----------|
| `Assets/Editor/FixPanelRowLayout.cs` | 신규 생성 (1회성 Editor 스크립트) |
| `Assets/_Project/Scenes/Game.unity` | Row/Slot에 LayoutElement 추가 (씬 저장) |

---

## 위험 요소

- BuildingActionPanel은 이미 균등하므로 LayoutElement 추가가 기존 동작을 바꾸지 않는다 (값이 현재와 동일한 결과)
- Slot에 LayoutElement를 추가해도 내부 IconImage 크기에는 영향 없음 (Slot 크기만 균등해짐)
- ProductionPopup BorderOverlay(IgnoreLayout=True)는 수정 불필요 — Row 크기가 균등해지면 자동으로 균등해짐
