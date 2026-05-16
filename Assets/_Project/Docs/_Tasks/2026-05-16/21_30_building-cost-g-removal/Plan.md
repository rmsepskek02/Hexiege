# Plan — 건물 배치 패널 비용 텍스트 'G' 제거

> **이 작업이 하려는 것:**
> 건물 배치 팝업에서 비용 텍스트에 붙어 있는 'G' 접미사를 제거한다.
> 유닛 생산 패널은 이미 숫자만 표시하고 있으므로, 건물 배치 패널도 동일한 표기 방식으로 통일한다.

---

> **GameSystemRules.md 검토 결과:**
> 현재 GameSystemRules.md에 건물 배치 패널 UI 섹션이 없다.
> 이번 작업 완료 후 신규 섹션을 추가하며, 비용 텍스트 표기 형식 규칙도 함께 추가한다.

---

## 수정 파일

**[BuildingPlacementUI.cs](Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs)**

### 변경 1 — Line 174 (Show 내부)

```
현재: _buildingCostTexts[i].SetText($"{cost}G");
변경: _buildingCostTexts[i].SetText($"{cost}");
```

### 변경 2 — Line 301 (UpdateBuildingStatsText 내부)

```
현재: _buildingCostTexts[i].SetText($"{cost}G");
변경: _buildingCostTexts[i].SetText($"{cost}");
```

---

## 위험 요소

없음. 단순 문자열 포맷 변경이며, 로직·이벤트·색상 처리에 영향 없음.
