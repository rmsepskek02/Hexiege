# Research — UI 색상 일관성 + 레이아웃 수정

게임 화면 UI TC 실기기 테스트(2026-05-27) 결과 FAIL로 판정된 레이아웃/색상 항목 9개를 수정합니다.
동시에 프로젝트 전체에서 색상이 제각각 하드코딩되어 있는 문제를 해결하기 위해
`UIColorConfig` ScriptableObject를 도입하여 색상을 한 곳에서 관리할 수 있도록 합니다.

---

## 작업 배경

TC 실기기 테스트에서 FAIL 판정을 받은 항목 중 "버그"가 아닌 "UI 크기/레이아웃/색상 조정"에 해당하는 9개 항목이 남아있습니다.
이와 별도로, 프로젝트 코드를 분석한 결과 동일한 의미를 가진 색상(예: 골드 부족=빨강)이 여러 파일에 중복 선언되어 있어
나중에 색상을 변경할 때 모든 파일을 일일이 찾아 수정해야 하는 유지보수 문제가 존재합니다.

---

## FAIL 항목 목록

| TC | UI | 증상 |
|----|----|------|
| TC-SINGLE-HUD-007 | GameHudUI | 설정(톱니바퀴) 버튼 크기 작음, 우측 상단 패딩 조정 필요 |
| TC-SINGLE-BP-001 | BuildingPopup (BuildingPlacementUI) | 패널 높이 부족, 버튼이 테두리 침범, 버튼 크기 작음 |
| TC-SINGLE-BP-002 | BuildingPopup | 금화 아이콘(위)과 비용 숫자(아래)가 세로로 쌓인 구조에서 숫자가 아이콘 중심축 기준으로 좌우 치우침 |
| TC-SINGLE-PRD-001 | ProductionPopup (ProductionPanelUI) | BP-001과 동일 — 패널 높이, 버튼 크기/비율 조정 필요 |
| TC-SINGLE-BAP-001 | BuildingActionPanel (BuildingActionPanelUI) | 패널 높이 조정 필요, 닫기(X) 버튼 위치가 다른 패널과 상이 |
| TC-SINGLE-SET-004 | ConfirmPopup | 포기 확인 팝업 버튼 색상 없음 (확인=초록, 취소=빨강 필요) |
| TC-SINGLE-SET-007 | InGameSettingsPanel → GameEndPanel | 게임 종료 UI 크기 너무 작음 |
| TC-SINGLE-END-001 | GameEndPanel | SET-007과 동일 UI — 게임 종료 UI 크기 너무 작음 |
| TC-MULTI-END-002 | RematchRequestPopup | 재경기 요청 UI 크기 조정 필요 |

> SET-007과 END-001은 같은 GameEndPanel을 가리키므로 실제 수정 대상은 8개 UI입니다.

---

## 현재 색상 적용 방식 분석

프로젝트 내 색상이 3가지 방식으로 혼재하고 있습니다.

### 방식 1 — 하드코딩 static readonly (코드 내 상수 선언)

**파일:** `Presentation/UI/GameEndUI.cs:86-87`

```csharp
private static readonly Color WinColor = new Color(0.3f, 0.5f, 0.9f);   // 파랑
private static readonly Color LoseColor = new Color(0.9f, 0.3f, 0.3f);  // 빨강
```

승리/패배 결과 텍스트 색상이 코드 상수로 선언되어 있습니다.
색상을 바꾸려면 코드를 수정하고 재컴파일해야 합니다.

---

### 방식 2 — 하드코딩 직접 참조 (Color.red / Color.white / Color.green)

**GameHudUI.cs:201**
```csharp
_populationText.color = isFull ? Color.red : Color.white;
```
인구 만원 시 빨간색, 정상 시 흰색.

**BuildingPlacementUI.cs:359, 366, 441**
```csharp
_buildingCostTexts[i].color = Color.white;
_buildingCostTexts[i].color = (currentGold < cost) ? Color.red : Color.white;
```
골드 부족 시 빨간색.

**ProductionPanelUI.cs:567, 749, 753, 763**
```csharp
_unitCostTexts[i].color = (currentGold < cost) ? Color.red : Color.white;
_upgradeCostText.color = (currentGold < upCost) ? Color.red : Color.white;
```
골드 부족/업그레이드 비용 부족 시 빨간색.

**BuildingPanelBase.cs:234**
```csharp
_demolishRefundText.color = Color.green;
```
철거 환불 텍스트 초록색.

---

### 방식 3 — SerializeField (컴포넌트별 Inspector 설정)

**FloatingHpTextSpawner.cs:83-86**
```csharp
[SerializeField] private Color _blueTeamColor = new Color(120f/255f, 230f/255f, 80f/255f);
[SerializeField] private Color _redTeamColor  = new Color(255f/255f, 220f/255f, 30f/255f);
```
팀별 HP 텍스트 색상. 이미 Inspector에서 조정 가능한 구조이므로 UIColorConfig 범위에 포함하지 않습니다.

**TabBarView.cs:35-37**
```csharp
[SerializeField] private Color _normalColor   = new Color(0.7f, 0.7f, 0.7f, 1f);
[SerializeField] private Color _selectedColor = Color.white;
```
탭바 선택/비선택 색상. 로비 전용 UI로 범위 밖입니다.

---

### ConfirmPopup 버튼 색상 현황

`ConfirmPopup.cs`에는 버튼 색상을 설정하는 코드가 전혀 없습니다.
현재 확인/취소 버튼은 Unity 기본 흰색 버튼 상태이며, Inspector에서 Image 컴포넌트 색상을 수동으로 설정해야 합니다.
TC-SINGLE-SET-004 FAIL의 원인입니다.

---

## UIColorConfig 도입 범위

UIColorConfig에 포함할 색상 항목:

| 필드명 | 기본값 | 현재 사용처 |
|--------|--------|------------|
| `goldInsufficientColor` | 빨강 | BuildingPlacementUI, ProductionPanelUI |
| `normalTextColor` | 흰색 | 위 두 파일의 리셋 색상 |
| `populationFullColor` | 빨강 | GameHudUI |
| `winColor` | 파랑 | GameEndUI |
| `loseColor` | 빨강 | GameEndUI |
| `demolishRefundColor` | 초록 | BuildingPanelBase |
| `confirmButtonColor` | 초록 | ConfirmPopup (신규 적용) |
| `cancelButtonColor` | 빨강 | ConfirmPopup (신규 적용) |

---

## 아키텍처 검토

### UIColorConfig 배치 위치

기존 프로젝트의 ScriptableObject 패턴:
- `UnitStatsConfig.cs` → `Infrastructure/Config/`
- `BuildingStatsConfig.cs` → `Infrastructure/Config/`
- `ToastMessageConfig.cs` → `Infrastructure/Config/` (ToastUI가 직접 SerializeField로 보유)

`UIColorConfig`도 동일하게 `Infrastructure/Config/`에 배치합니다.
에셋 파일은 `Resources/Config/UIColorConfig.asset`에 생성합니다.

### 주입 방식

`ToastMessageConfig`의 패턴(각 컴포넌트가 `[SerializeField]`로 직접 에셋 참조)을 동일하게 적용합니다.
색상이 필요한 각 UI 컴포넌트에 `[SerializeField] private UIColorConfig _colorConfig` 필드를 추가하고,
Inspector에서 동일한 `UIColorConfig.asset`을 연결합니다.

GameBootstrapper를 통한 주입은 사용하지 않습니다 — 색상은 순수 시각 설정이므로 UI 컴포넌트가 직접 보유하는 것이 자연스럽습니다.

### ConfirmPopup 버튼 색상 적용 방식

ConfirmPopup.cs에 다음을 추가합니다:
- `[SerializeField] private Image _confirmButtonImage` — 확인 버튼의 배경 Image 참조
- `[SerializeField] private Image _cancelButtonImage` — 취소 버튼의 배경 Image 참조
- `[SerializeField] private UIColorConfig _colorConfig` — 색상 설정 참조
- `Awake()`에서 `_confirmButtonImage.color = _colorConfig.confirmButtonColor` 적용

버튼 색상은 Show() 시마다 바뀌지 않으므로 `Awake()`에서 1회 적용합니다.

### 레이어 의존 검토

`UIColorConfig`(ScriptableObject)는 Presentation 레이어 UI 컴포넌트가 SerializeField로 참조합니다.
ScriptableObject는 Unity 에셋이므로 레이어 경계와 무관하게 어디서나 참조 가능합니다.
`Infrastructure/Config/`에 배치하더라도 레이어 위반이 아닙니다.

---

## 정적 분석 체크포인트

- UIColorConfig 도입 후 기존 `Color.red`, `Color.white`, `Color.green`, `WinColor`, `LoseColor` 참조가 config 참조로 완전히 교체되었는지 Grep 검증 필요
- FloatingHpTextSpawner, TabBarView의 SerializeField Color는 범위 밖 — 수정 대상 아님
- ConfirmPopup의 Button Image 색상: ColorBlock의 Normal Color와 Image.color의 관계 주의 (Normal Color=흰색 유지 시 Image.color가 최종 색상 결정)
