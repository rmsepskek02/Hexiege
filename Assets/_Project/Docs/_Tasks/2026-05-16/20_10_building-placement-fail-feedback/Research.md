# Research — 건물 배치 패널 실패 피드백

> **이 작업이 필요한 이유:**
> 건물 배치 팝업에서 건물 버튼을 눌렀을 때 골드가 부족하면 현재 아무 반응이 없다.
> 유닛 생산 패널에서 이미 구현된 것처럼, 골드 비용 텍스트를 빨간색으로 표시하고
> 토스트 메시지를 통해 실패 원인을 즉각적으로 알려주도록 개선한다.

---

## 대상 파일

- `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

---

## 현재 상태 분석

### 팝업 흐름
1. 자기 팀 빈 타일 탭 → `BuildingPlacementUI.Show(coord, team)` 호출
2. Show() 내부에서 각 건물 버튼에 비용 텍스트 설정 + 배치 가능 여부로 `interactable` 세팅
3. 플레이어가 버튼 탭 → `PlaceAndClose(BuildingType type)` 호출
4. Background 터치 또는 취소 버튼 → `Close()` 호출

### 골드 부족 시 현재 동작

**싱글플레이 분기 (`PlaceAndClose` line 319~323)**
```csharp
int cost = GetBuildingCost(type);
if (!_resource.CanAfford(_currentTeam, cost))
{
    return; // 골드 부족 → 배치하지 않음 (아무 피드백 없음, 팝업 유지)
}
```

**멀티플레이 분기 (`PlaceAndClose` line 295~303)**
```csharp
if (!_resource.CanAfford(_currentTeam, cost))
{
    Debug.Log(...); // 로그만 출력
    Close();        // 팝업 닫힘 (토스트 없음)
    return;
}
```

### 비용 텍스트 관련 필드
- `_buildingCostTexts: List<TextMeshProUGUI>` — 각 건물 버튼의 비용 텍스트 리스트
- Show() → 버튼 순회 시 `_buildingCostTexts[i].SetText($"{cost}G")` 으로 내용만 설정, 색상은 건드리지 않음
- `UpdateBuildingStatsText()` 메서드가 존재하나 **실제로 호출되는 곳 없음** (미사용 상태)

### OnResourceChanged 이벤트
- `GameEvents.OnResourceChanged` — `Subject<ResourceChangedEvent>` (UniRx)
- 골드가 변경될 때마다 발행 (SpendGold, AddGold 모두 포함)
- `ResourceChangedEvent.Team: TeamId` 필드로 팀 필터링 가능
- **ProductionPanelUI는 Initialize()에서 이 이벤트를 구독하여 항상 실시간 갱신**
- BuildingPlacementUI는 현재 이 이벤트를 구독하지 않음

### 건물 배치 가능 여부 (`CanPlaceBuildingType`)
- Show() 시 `_buildingButtons[i].interactable = _buildingPlacement.CanPlaceBuildingType(entry.type, coord, team)` 으로 이미 비활성화 처리됨
- 골드 부족과 무관하게 영토 밖, 중복 배치 등으로도 interactable=false 처리
- **골드 부족은 CanPlaceBuildingType에 포함되지 않음** — 별도 체크 필요

### ToastKey 현황
- `ToastKey.GoldInsufficient` — 이미 정의됨, 유닛 생산 패널에서 사용 중
- **새 키 추가 없이 기존 키 재사용 가능**

---

## 유닛 생산 패널과의 차이점

| 항목 | 유닛 생산 패널 (기구현) | 건물 배치 패널 (이번 작업) |
|------|----------------------|--------------------------|
| 실패 종류 | 큐 초과 / 골드 부족 / 인구 초과 | 골드 부족만 |
| 비용 텍스트 | 항상 실시간 갱신 (항상 열려있음) | 팝업 열린 동안만 갱신 |
| 골드 변경 이벤트 구독 | Initialize()에서 영구 구독 | Show()~Close() 사이만 구독 |
| 골드 부족 시 팝업 | 유지 (닫지 않음) | 유지 (닫지 않음 — 싱글플레이 한정) |
