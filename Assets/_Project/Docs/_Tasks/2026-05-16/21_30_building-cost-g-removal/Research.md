# Research — 건물 배치 패널 비용 텍스트 'G' 제거

> **이 작업이 하려는 것:**
> 건물 배치 팝업에서 각 건물 버튼 아래 표시되는 골드 비용 텍스트에 붙어 있는 'G' 접미사를 제거한다.
> 예: "200G" → "200"
> 유닛 생산 패널의 비용 텍스트는 이미 숫자만 표시하고 있으며, 이번 작업으로 건물 배치 패널도 동일하게 맞춘다.

---

## 대상 파일

- `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

---

## 현재 상태

### 'G'가 추가되는 위치 (2곳)

**Line 174 — Show() 내부 버튼 초기화 루프**
```csharp
_buildingCostTexts[i].SetText($"{cost}G");
```
팝업이 열릴 때 각 건물 버튼의 비용 텍스트를 설정하는 부분.

**Line 301 — UpdateBuildingStatsText()**
```csharp
_buildingCostTexts[i].SetText($"{cost}G");
```
팀/종족별 비용 텍스트를 업데이트하는 메서드 내부.
현재 이 메서드는 코드에만 존재하며, 실제 호출되는지 여부는 미확인.
(미사용이더라도 일관성을 위해 동일하게 수정한다.)

---

## 참고 — 유닛 생산 패널과의 비교

PROJECT_STATUS.md에 이미 "생산 패널 골드 비용 텍스트 표기 — 숫자만, G 없음"으로 기록되어 있다.
건물 배치 팝업만 누락된 상태였다.
