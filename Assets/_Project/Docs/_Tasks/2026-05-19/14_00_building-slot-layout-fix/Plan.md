# Plan: 건물 배치 팝업 — 3행 버튼 크기 균일화

## 이 문서가 다루는 것

BuildingPlacementUI에서 3행 버튼 1개가 가로폭 전체를 차지하는 레이아웃 버그를 수정하는 작업 계획서입니다.

쉽게 말하면: 빈 슬롯을 `SetActive(false)` (레이아웃에서 아예 제거)로 숨기는 대신, `CanvasGroup.alpha = 0` (레이아웃 공간은 유지하되 눈에 안 보임)으로 숨기도록 바꿉니다. 이렇게 하면 3행에도 3개 슬롯의 공간이 항상 확보되어 버튼 크기가 1·2행과 동일해집니다.

> 본 Plan은 **계획 문서**이며 어떤 코드도 직접 수정하지 않았습니다.

---

## ⚠️ 기존 로직 제거 항목

해당 없음. 기존 SetActive 호출은 CanvasGroup 방식으로 **교체**되며, 삭제가 아닌 대체입니다.

---

## GameSystemRules.md 근거

**"건물 배치 패널 UI" 섹션** 전반에 근거합니다.

- **규칙 2** (색상 표시 기준): 팝업이 열린 동안 골드 비교 로직이 올바르게 동작해야 함. CanvasGroup 방식으로 바꿔도 비활성 슬롯을 색상 재평가에서 건너뛰는 로직이 유지되어야 함.
- **규칙 3** (색상 재평가 시점): 팝업 열림과 골드 변동 시 재평가 로직에 영향 없어야 함.
- **UI 완성도 원칙** (CLAUDE.md 7번): 1·2행과 3행의 버튼 크기가 균일해야 완성도 있는 UI로 볼 수 있으므로 수정이 필요함.

---

## 접근 방법

### 핵심 변경: SetActive → CanvasGroup

**기존 방식 (문제)**
```csharp
// 빈 슬롯을 비활성화 → 레이아웃에서 완전히 제거됨
_buildingButtons[i].gameObject.SetActive(false);
```

**변경 방식 (해결)**
```csharp
// 빈 슬롯을 보이지 않게만 처리 → 레이아웃 공간 유지
_buttonCanvasGroups[i].alpha = 0f;
_buttonCanvasGroups[i].interactable = false;
_buttonCanvasGroups[i].blocksRaycasts = false;
```

활성 슬롯:
```csharp
_buttonCanvasGroups[i].alpha = 1f;
_buttonCanvasGroups[i].interactable = true;
_buttonCanvasGroups[i].blocksRaycasts = true;
```

### CanvasGroup 캐시 방식

- 별도 Inspector 설정 없이 코드에서 자동 처리
- `Initialize()` 또는 `Awake()` 시점에 `_buildingButtons` 리스트를 순회하며 CanvasGroup을 가져오거나 없으면 추가 (`GetComponent` → null이면 `AddComponent`)
- `List<CanvasGroup> _buttonCanvasGroups` 필드로 캐시

### UpdateCostTextColors 조건 수정

기존 조건:
```csharp
if (!_buildingButtons[i].gameObject.activeSelf || _buildingCostTexts[i] == null)
    continue;
```

변경 조건:
```csharp
if (_buttonCanvasGroups[i].alpha < 0.5f || _buildingCostTexts[i] == null)
    continue;
```
(alpha=0이면 빈 슬롯으로 판단해 건너뜀)

---

## 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Presentation/UI/BuildingPlacementUI.cs` | ① `List<CanvasGroup> _buttonCanvasGroups` 필드 추가, ② `Initialize()` 또는 `Awake()`에서 CanvasGroup 자동 추가·캐시, ③ `Show()` 내 `SetActive(false/true)` → CanvasGroup 방식으로 교체, ④ `UpdateCostTextColors()`의 activeSelf 조건 → alpha 조건으로 교체 | 수정 |

Inspector 변경: **없음** (CanvasGroup은 코드에서 자동 추가)

---

## 위험 요소

| 위험 | 해결 방법 |
|------|-----------|
| `GetComponent<CanvasGroup>()`가 실패하고 `AddComponent`가 뒤늦게 호출되면 첫 Show() 시 캐시가 없을 수 있음 | `Awake()`에서 미리 캐시 → Show()는 캐시만 사용 |
| CanvasGroup이 이미 부모 오브젝트에 붙어 있어 버튼 자체에 추가한 CanvasGroup과 간섭할 가능성 | 버튼 GameObject 직접 체크 (`GetComponentInParent` 아닌 `GetComponent`) |
| `UpdateCostTextColors`의 alpha 조건이 애니메이션 중(투명도 전환 중)에 오동작 가능성 | 현재 SetActive 방식도 동일 문제 발생 가능. alpha < 0.5f 임계값으로 충분히 대응 가능. |
| 씬 직렬화 데이터(Game.unity)의 Slot 초기 상태(m_IsActive: 1)와 충돌 가능성 | Awake에서 모든 슬롯을 alpha=0으로 초기화 → Show() 호출 시 올바르게 설정됨. 초기 상태 문제 없음. |

---

## 검증 체크리스트

- [ ] Human 종족(7개 건물) — 3행 버튼 1개가 다른 버튼들과 동일한 크기로 표시되는지 확인
- [ ] Spirit 종족(7개 건물) — 동일 확인
- [ ] Transcendence 종족(8개 건물) — 3행 버튼 2개가 동일한 크기로 표시되는지 확인
- [ ] 건물 버튼 클릭 → 건물이 정상적으로 배치되는지 확인
- [ ] 비어있는 슬롯 영역 클릭 시 반응 없는지 확인 (blocksRaycasts=false)
- [ ] 골드 부족 시 비용 텍스트 빨간색 표시 확인 (UpdateCostTextColors 로직 정상 동작)
- [ ] 팝업 닫기 후 재오픈 시 버튼 상태 정상 초기화 확인

---

## 버그 수정 — 최초 타일 클릭 시 팝업 내용 미표시 (2026-05-19)

### 원인

구현 직후 발견된 버그. 최초 타일 클릭 시 건물 버튼이 전혀 보이지 않고, 두 번째 클릭부터 정상 표시됨.

`BuildingPlacementUI` 컴포넌트가 `BuildingPopup` 오브젝트(`m_IsActive: 0`) 위에 부착되어 있어, 씬 로드 시점에 `Awake()`가 실행되지 않음. 그 결과:

1. 첫 클릭 → `Show()` 호출 → `_buttonCanvasGroups == null` → alpha 설정 전부 건너뜀
2. `_popup.Show()` → `SetActive(true)` → 이 시점에 `Awake()` 실행 → 모든 슬롯 alpha=0 초기화
3. 결과: 팝업은 열렸지만 빈 화면

### 해결 방법 — CanvasGroup 초기화를 `Initialize()`로 이전

`Initialize()`는 GameBootstrapper가 호출하는 일반 C# 메서드이므로, 오브젝트가 비활성 상태여도 정상 실행됨.

| 변경 전 | 변경 후 |
|---------|---------|
| `Awake()`에서 CanvasGroup 캐시 | `Initialize()`에서 CanvasGroup 캐시 |
| 비활성 시 실행 안 됨 | 항상 실행 보장 |

`Awake()`는 제거하거나 빈 메서드로 유지.

### 변경 파일

| 파일 | 변경 내용 |
|------|-----------|
| `Presentation/UI/BuildingPlacementUI.cs` | `Awake()` 제거, `Initialize()` 내부에 CanvasGroup 캐시 코드 이전 |
