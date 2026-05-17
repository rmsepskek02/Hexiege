# Research.md — ProductionPopup UI 레이아웃 재구성

## 작업 개요 (자연어)

생산 건물을 클릭하면 나타나는 ProductionPopup의 버튼 레이아웃을 재구성하는 UI 작업이다.

현재 6개의 버튼이 모두 유닛 생산 버튼으로 임시 구성되어 있다.
이번 작업에서는 위 3개(유닛 생산)와 아래 3개(랠리/업그레이드/철거)로 역할을 나눈다.

철거 버튼은 UI만 추가하고 실제 철거 동작은 이번 범위에서 제외한다.

---

## 현재 코드 구조 — ProductionPanelUI.cs

### Inspector 필드 현황

| 필드 | 타입 | 개수 | 설명 |
|------|------|------|------|
| `_unitButtons` | List\<Button\> | 6 | 유닛 생산 버튼 (전부 유닛용) |
| `_unitButtonPortraits` | List\<Image\> | 6 | 유닛 초상화 |
| `_unitCostTexts` | List\<TMP\> | 6 | 유닛 비용 텍스트 |
| `_unitAutoIndicators` | List\<GO\> | 6 | 자동 생산 인디케이터 |
| `_unitLockIndicators` | List\<GO\> | 6 | 잠금 인디케이터 |
| `_rallyPointButton` | Button | 1 | 별도 배치된 랠리 버튼 |
| `_upgradeButton` | Button | 1 | 별도 배치된 업그레이드 버튼 |
| `_upgradeCostText` | TMP | 1 | 업그레이드 비용 텍스트 |

> 현재 `_rallyPointButton`과 `_upgradeButton`은 6개 버튼 그리드 외부에 별도로 배치되어 있음.
> 이번 작업에서 이 두 버튼을 그리드 하단 슬롯(좌·중)으로 재배선한다.

### 주요 메서드 현황

| 메서드 | 역할 | 변경 필요 여부 |
|--------|------|----------------|
| `Initialize()` | 버튼 이벤트 연결, 의존성 주입 | ✅ 철거 버튼 리스너 추가 |
| `Show(BuildingData)` | 팝업 열기, 버튼 바인딩 | ✅ 철거 환불 표시 추가 |
| `UpdateUpgradeButton(race)` | 업그레이드 가능 여부 갱신 | ✅ 숨김 방식 + 아이콘 교체 |
| `BindButtonUnitTypes(race)` | 유닛 버튼 타입 바인딩 | ❌ (3개로 줄어도 로직 동일) |
| `UpdateButtonPortraits()` | 유닛 초상화 이미지 설정 | ❌ |
| `UpdateLockIndicators()` | 잠금 오버레이 표시 | ❌ (코드 변경 없음) |
| `UpdateInfoBar()` | 골드/인구 텍스트 갱신 | ❌ |

---

## 아이콘 파일 현황

경로: `Assets/_Project/Sprites/UI/Icons/`

| 파일명 | 사용처 | 존재 여부 |
|--------|--------|-----------|
| `ui_icon_rallypoint.png` | 랠리 버튼 아이콘 | ✅ |
| `ui_icon_destroy.png` | 철거 버튼 아이콘 | ✅ |
| `ui_icon_lock.png` | 잠금 유닛 오버레이 | ✅ |
| `ui_icon_gold.png` | 기존 골드 아이콘 | ✅ |

---

## 업그레이드 아이콘 — 다음 단계 건물 Sprite

- 현재 `ProductionPanelUI`에 건물 Sprite 매핑이 없음.
- `BuildingTypeHelper.GetNextStage(type)`으로 다음 BuildingType을 조회할 수 있음.
- → 새 매핑 리스트 `_buildingUpgradeIcons` 추가 필요.
  ```csharp
  // BuildingType → 해당 건물의 Sprite를 연결하는 구조체
  [System.Serializable]
  public struct BuildingIconEntry
  {
      public BuildingType buildingType;
      public Sprite icon;
  }
  [SerializeField] private List<BuildingIconEntry> _buildingUpgradeIcons;
  ```
- Show() 시점에 `GetNextStage(currentType)`으로 다음 타입을 얻고 리스트에서 Sprite를 조회해 설정.

---

## 업그레이드 버튼 숨김 처리 — 레이아웃 문제

**현재 방식:** `_upgradeButton.gameObject.SetActive(false)`
- Grid Layout Group 환경에서 SetActive(false)를 사용하면 해당 슬롯이 사라지고 다른 버튼이 이동함.
- 요구사항: 업그레이드 버튼이 숨겨도 랠리·철거 버튼의 크기/위치는 변하지 않아야 함.

**해결 방식:** `CanvasGroup` 컴포넌트 활용
```
업그레이드 버튼 GO에 CanvasGroup 추가
  업그레이드 가능:   alpha = 1, blocksRaycasts = true
  업그레이드 불가:   alpha = 0, blocksRaycasts = false
```
- SetActive는 사용하지 않으므로 레이아웃 공간이 그대로 유지됨.
- `_upgradeButtonGroup`: 업그레이드 버튼 GO의 CanvasGroup 참조 필드 추가.

---

## 철거 환불 금액 표시 (이번 범위: 표시만)

- 환불 금액 계산식: `BuildingStats.GetGoldCost(type, race) / 2`
- 건물마다 종족마다 건설 비용이 다르므로 `Show()` 시점에 계산.
- 텍스트 색상: **초록색** (`Color.green`).
- 실제 철거 동작은 이번 범위 외. 버튼 클릭 시 별도 처리 없음.

---

## 랠리 버튼 골드 표시 영역 숨김

- 랠리는 비용 없음 → 골드 아이콘 + 골드 텍스트를 표시할 필요 없음.
- 골드 표시 영역의 부모 GameObject를 `_rallyGoldDisplay` 필드로 참조.
- `Initialize()` 시점에 한 번 `SetActive(false)` 처리 (이후 변경 없음).
- 랠리 버튼 아이콘(`ui_icon_rallypoint`)은 버튼 중앙에 배치.

---

## 영향 범위 요약

| 항목 | 변경 여부 |
|------|-----------|
| `ProductionPanelUI.cs` | ✅ 변경 (Inspector 필드 추가, 메서드 수정) |
| Game.unity 씬 Inspector | ✅ 재구성 필요 (버튼 재배선, 신규 필드 연결) |
| `BuildingTypeHelper.cs` | ❌ 변경 없음 |
| `BuildingStats.cs` | ❌ 변경 없음 |
| `InputHandler.cs` | ❌ 변경 없음 |
| 기타 파일 | ❌ 변경 없음 |
