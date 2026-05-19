# Research: 건물 배치 팝업 — 3행 버튼 크기 불일치

## 이 문서가 다루는 것

건물 배치 팝업(BuildingPopup)에서 3행 버튼이 1·2행 버튼보다 가로폭이 훨씬 넓게 표시되는 현상의 원인을 분석한 보고서입니다.

---

## 현상

- Human / Spirit 종족은 건물이 7개 (3+3+1 배치)
- 1행·2행: 버튼 3개가 동일한 너비로 표시 ✅
- 3행: 버튼 1개가 전체 가로폭을 차지 ❌

---

## UI 계층 구조 (씬 파악 결과)

```
BuildingPanel (601365892)
└── VerticalLayoutGroup 컨테이너 (1399166241)
    ├── BuildingButtons1 (239298193)  ← HorizontalLayoutGroup, 3 자식 (Slot1~3)
    ├── BuildingButtons2 (691910871)  ← HorizontalLayoutGroup, 3 자식 (Slot4~6)
    └── BuildingButtons3 (426929633)  ← HorizontalLayoutGroup, 3 자식 (Slot7~9)
```

### VerticalLayoutGroup 설정 (1399166241)
- `m_ChildForceExpandWidth: 0`
- `m_ChildControlWidth: 1`
- Spacing: 15

### 각 행의 HorizontalLayoutGroup 설정 (BuildingButtons1/2/3 모두 동일)
- `m_ChildForceExpandWidth: 1` ← **핵심**
- `m_ChildControlWidth: 1`
- Spacing: 8, Padding: L8 R8 T0 B0

---

## 근본 원인

`BuildingPlacementUI.cs` [line 188](../../Scripts/Presentation/UI/BuildingPlacementUI.cs#L188):

```csharp
// 건물 타입이 없는 슬롯 숨김
_buildingButtons[i].gameObject.SetActive(false);
```

Unity의 Layout Group은 **`SetActive(false)` 상태인 자식을 레이아웃 계산에서 완전히 제외**합니다.

Human/Spirit 종족은 7개 건물이므로, 9개 슬롯 중 Slot8과 Slot9가 `SetActive(false)`가 됩니다.  
3행(BuildingButtons3)에서 Slot7만 active 상태가 되고,  
HorizontalLayoutGroup의 `ChildForceExpandWidth = 1` 설정이 그 하나의 슬롯을 전체 너비로 늘립니다.

### 정리

| 행 | 활성 슬롯 | 결과 |
|----|-----------|------|
| BuildingButtons1 | Slot1, Slot2, Slot3 (3개) | 각 1/3 너비 ✅ |
| BuildingButtons2 | Slot4, Slot5, Slot6 (3개) | 각 1/3 너비 ✅ |
| BuildingButtons3 | Slot7만 active (8·9는 SetActive=false) | Slot7이 전체 너비 ❌ |

---

## UpdateCostTextColors 연관 코드

`BuildingPlacementUI.cs` line 215 주석:
```csharp
/// - 버튼이 비활성(SetActive=false)이거나 텍스트가 null이면 건너뜀.
```

`SetActive` 방식을 바꿀 경우 이 로직도 함께 수정해야 함.

---

## 영향 범위

| 파일 | 변경 대상 |
|------|-----------|
| `Presentation/UI/BuildingPlacementUI.cs` | `Show()` 내 SetActive 교체, `UpdateCostTextColors()` 조건 수정, CanvasGroup 캐시 추가 |

Inspector 변경은 필요 없음 — 코드에서 Awake/Initialize 시점에 CanvasGroup을 자동으로 추가 및 캐시.

---

## 관련 파일

- `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`
- `Assets/_Project/Scenes/Game.unity` (BuildingPanel 씬 오브젝트)
