# Plan — 건물 배치 패널 실패 피드백

> **이 작업이 하려는 것:**
> 건물 배치 팝업에서 골드가 부족할 때 사용자가 즉시 상황을 파악할 수 있도록 두 가지를 추가한다.
> 첫째, 팝업이 열리는 순간부터 현재 골드로 살 수 없는 건물의 비용 텍스트를 빨간색으로 표시한다.
> 둘째, 빨간색 텍스트인 건물 버튼을 눌렀을 때 "골드가 부족합니다" 토스트 메시지를 표시한다.

---

> **GameSystemRules.md 검토 결과:**
> 현재 GameSystemRules.md에는 건물 배치 패널 피드백에 관한 규칙이 없다.
> 이번 작업 완료 후 새 규칙 섹션을 추가한다.

---

## 구현 목표

| 상황 | UI 반응 | 토스트 |
|------|---------|--------|
| 팝업 열릴 때 골드 부족 건물 존재 | 해당 건물 비용 텍스트 빨간색 | 없음 |
| 팝업 열린 상태에서 골드 변경 | 비용 텍스트 색상 실시간 재평가 | 없음 |
| 골드 부족 건물 버튼 탭 (싱글플레이) | — | O ("골드가 부족합니다") |
| 팝업 닫힐 때 | 비용 텍스트 흰색으로 초기화 | — |

> **멀티플레이 분기는 이번 작업 범위 밖.** 싱글플레이 분기(`_resource.CanAfford` 직접 체크)에만 피드백 적용.

---

## 구현 계획

### 수정 파일 1개

**[BuildingPlacementUI.cs](Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs)**

---

### [1] 이벤트 구독 — Show()/Close() 생명주기에 연동

#### 현재
BuildingPlacementUI는 `OnResourceChanged` 이벤트를 구독하지 않는다.

#### 변경

`_resourceSubscription: IDisposable` 필드를 추가하고,

**Show()** 마지막에:
```
OnResourceChanged 구독 시작
  → 이벤트 발생 시 자기 팀 이벤트인지 확인
  → 맞으면 UpdateCostTextColors() 호출
```

**Close()** 시작에:
```
_resourceSubscription?.Dispose()
_resourceSubscription = null
```

구독 기간을 팝업이 열린 동안으로 한정하여 불필요한 이벤트 처리를 방지한다.

---

### [2] `UpdateCostTextColors()` — 신규 private 메서드

팝업에 표시 중인 각 건물 버튼의 비용 텍스트 색상을 현재 골드와 비교하여 설정한다.

```
_buildingCostTexts[i] 순회:
  현재 건물 타입 조회 (GetBuildingList + 현재 팀/종족으로 인덱스 매핑)
  BuildingStats.GetGoldCost(type, race) 비용 조회
  _resource.GetGold(_currentTeam) 현재 골드 조회
  보유 골드 < 비용 → Color.red
  보유 골드 >= 비용 → Color.white
```

**주의:**
- 버튼이 비활성(SetActive=false)인 경우 건너뜀
- `_buildingCostTexts[i] == null`이면 건너뜀
- 현재 팀/종족 정보를 Show()에서 저장한 `_currentTeam` 필드와 `GameRaceContext`로 조회

---

### [3] `Show()` — 팝업 열릴 때 즉시 색상 평가

기존 Show() 마지막에 `UpdateCostTextColors()` 호출 추가.
비용 텍스트 내용 설정(`SetText`)이 먼저 완료된 후 색상 평가가 이루어지도록 순서 보장.

---

### [4] `PlaceAndClose()` — 싱글플레이 골드 부족 분기에 토스트 추가

```csharp
현재:
    if (!_resource.CanAfford(_currentTeam, cost))
    {
        return;
    }

변경:
    if (!_resource.CanAfford(_currentTeam, cost))
    {
        ToastUI.Show(ToastKey.GoldInsufficient);
        return;  // 팝업 유지 (Close 호출 없음)
    }
```

팝업을 닫지 않는 이유: 사용자가 골드 텍스트 색상을 보며 어떤 건물이 불가능한지 파악할 수 있다.

---

### [5] `Close()` — 비용 텍스트 색상 초기화

팝업이 닫힐 때 모든 비용 텍스트를 흰색으로 복구한다.
다음 Show() 시 UpdateCostTextColors()가 재평가하므로 초기화가 필수.

```
Close() 시작에:
    _resourceSubscription?.Dispose()
    _resourceSubscription = null
    _buildingCostTexts 순회 → 각 색상 Color.white 초기화
```

---

## GameSystemRules.md 추가 규칙

작업 완료 후 `GameSystemRules.md`에 **건물 배치 패널 UI** 섹션을 신규 추가한다.

```
## 건물 배치 패널 UI

**규칙 1. 건물 비용 텍스트 색상**
팝업이 열린 동안 현재 보유 골드가 해당 건물의 건설 비용보다 적으면 빨간색으로 표시된다.
충분하면 흰색으로 표시된다.
팝업이 열리는 순간과 골드가 변경될 때마다 자동으로 재평가된다.
팝업이 닫히면 흰색으로 초기화된다.

**규칙 2. 건물 배치 실패 피드백**
골드가 부족한 상태에서 건물 버튼을 탭하면 토스트 메시지를 표시하고 팝업을 유지한다.
```

---

## 위험 요소

| 위험 | 설명 | 대응 |
|------|------|------|
| 버튼 인덱스와 비용 텍스트 인덱스 불일치 | `_buildingButtons`와 `_buildingCostTexts`가 1:1 매핑이므로 Show()에서 이미 같은 인덱스로 처리됨 | UpdateCostTextColors()에서 동일 GetBuildingList() 사용 |
| 팝업 닫힌 후 이벤트 수신 | Show/Close 생명주기 바깥에서 이벤트가 들어올 수 있음 | Close()에서 즉시 Dispose() |
| GetGold() 미노출 | ResourceUseCase에 GetGold(team) 공개 메서드 필요 | 이미 존재함 확인 필요 → game-programmer 에이전트가 확인 |
