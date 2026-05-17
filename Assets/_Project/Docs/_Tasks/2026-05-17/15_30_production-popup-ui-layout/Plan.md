# Plan.md — ProductionPopup UI 레이아웃 재구성

## 작업 개요 (자연어)

생산 건물 팝업(ProductionPopup)의 버튼 레이아웃을 2행 3열로 재구성한다.
위 3개는 유닛 생산 버튼, 아래 3개는 랠리/업그레이드/철거 액션 버튼으로 역할을 분리한다.
코드 변경은 `ProductionPanelUI.cs` 단일 파일이며, Inspector 재구성이 함께 필요하다.
철거 버튼은 UI와 환불 금액 표시만 추가하며, 실제 철거 동작은 이번 범위에서 제외한다.

---

## GameSystemRules.md 근거

| 수정 항목 | 근거 규칙 |
|-----------|-----------|
| 철거 버튼 추가 | 건물 철거 시스템 규칙 2: "건물 클릭 시 열리는 팝업에 철거 버튼을 포함한다." |
| 철거 환불 금액 표시 | 건물 철거 시스템 규칙 4: "철거 시 건설 비용 50%를 즉시 환불한다." |
| 랠리 골드 표시 제거 | 생산 패널 UI — 랠리는 비용 없음 (규칙상 골드 소비 없음) |
| 업그레이드 버튼 표시 조건 | 생산 패널 UI — 업그레이드 시스템 연동 (BuildingTypeHelper.CanUpgrade) |

---

## 최종 UI 레이아웃

```
┌─────────────────────────────────────┐
│           유닛1  유닛2  유닛3        │
│          [btn] [btn] [btn]          │
│                                     │
│          랠리  업그레이드  철거       │
│          [btn]   [btn]   [btn]      │
│                                     │
│     [큐슬롯0] [큐슬롯1] [큐슬롯2]   │  ← 기존 _queueSlotImages, 변경 없음
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━ (진행 바)   │
│  🪙 9999   👥 99/99                 │
└─────────────────────────────────────┘
```

각 액션 버튼의 하단 비용 표시:
- 랠리: 표시 없음 (골드 아이콘 + 텍스트 영역 숨김)
- 업그레이드: 업그레이드 비용 (흰색/빨간색)
- 철거: 환불 금액 (초록색)

---

## 변경 파일: ProductionPanelUI.cs

### [1] 새 Inspector 필드 추가

```csharp
// 기존 [Header("Upgrade")] 섹션 아래에 추가

[Header("Action Buttons")]
[Tooltip("철거 버튼. 클릭 로직은 별도 작업 예정.")]
[SerializeField] private Button _demolishButton;

[Tooltip("업그레이드 버튼에 표시될 아이콘 Image (다음 단계 건물 Sprite를 런타임에 할당).")]
[SerializeField] private Image _upgradeIconImage;

[Tooltip("업그레이드 버튼에 부착된 CanvasGroup. alpha=0으로 숨겨도 레이아웃 공간 유지.")]
[SerializeField] private CanvasGroup _upgradeButtonGroup;

[Tooltip("랠리 버튼 하단의 골드 표시 영역(부모 GO). Initialize 시점에 비활성화.")]
[SerializeField] private GameObject _rallyGoldDisplay;

[Tooltip("철거 버튼 하단에 표시되는 환불 금액 텍스트. 초록색으로 표시.")]
[SerializeField] private TextMeshProUGUI _demolishRefundText;

// 업그레이드 아이콘 조회용 — 다음 단계 BuildingType과 Sprite를 매핑
[System.Serializable]
public struct BuildingIconEntry
{
    [Tooltip("대상 건물 타입.")]
    public BuildingType buildingType;
    [Tooltip("해당 건물의 아이콘 Sprite.")]
    public Sprite icon;
}

[Header("Building Icons (업그레이드 아이콘용)")]
[Tooltip("BuildingType별 건물 아이콘. 업그레이드 버튼에 다음 단계 건물 이미지를 설정할 때 사용.")]
[SerializeField] private List<BuildingIconEntry> _buildingUpgradeIcons;
```

---

### [2] Initialize() 수정

추가할 내용:
```csharp
// 철거 버튼 이벤트 연결 (로직은 이번 범위 외 — 빈 핸들러)
if (_demolishButton != null)
    _demolishButton.onClick.AddListener(OnDemolishButtonClick);

// 랠리 버튼 골드 표시 영역 영구 비활성화 (비용 없음)
if (_rallyGoldDisplay != null)
    _rallyGoldDisplay.SetActive(false);
```

---

### [3] Show() 수정

추가할 내용:
```csharp
// 철거 환불 금액 표시 (Show() 내 UpdateUpgradeButton 호출 직후)
UpdateDemolishRefund(race);
```

---

### [4] UpdateUpgradeButton() 수정

**현재:**
```csharp
if (_upgradeButton != null)
    _upgradeButton.gameObject.SetActive(canUpgrade);  // ← 레이아웃 이동 발생
```

**변경 후:**
```csharp
// CanvasGroup으로 숨김 — SetActive 대신 alpha=0으로 레이아웃 공간 유지
if (_upgradeButtonGroup != null)
{
    _upgradeButtonGroup.alpha = canUpgrade ? 1f : 0f;
    _upgradeButtonGroup.blocksRaycasts = canUpgrade;
    _upgradeButtonGroup.interactable = canUpgrade;
}

// 업그레이드 가능한 경우: 다음 단계 건물 아이콘 설정
if (canUpgrade && _upgradeIconImage != null)
{
    BuildingType? nextType = BuildingTypeHelper.GetNextStage(_currentBarracks.Type);
    if (nextType.HasValue)
    {
        Sprite icon = GetBuildingIcon(nextType.Value);
        if (icon != null)
            _upgradeIconImage.sprite = icon;
    }
}
```

기존 `_upgradeButton.gameObject.SetActive(canUpgrade)` 라인 제거.
기존 `_upgradeCostText.gameObject.SetActive(canUpgrade)` 라인은 유지 (비용 텍스트는 별도 처리).

---

### [5] UpdateDemolishRefund() 신규 추가

```csharp
/// <summary>
/// 철거 버튼 하단에 환불 예정 금액을 표시한다.
/// 환불 금액 = 건설 비용 50%. 텍스트 색상은 초록색으로 고정.
/// 실제 철거 로직은 이번 범위 외이므로 표시만 처리한다.
/// </summary>
private void UpdateDemolishRefund(RaceId race)
{
    if (_demolishRefundText == null || _currentBarracks == null) return;

    int buildCost = BuildingStats.GetGoldCost(_currentBarracks.Type, race);
    int refund = buildCost / 2;

    _demolishRefundText.text = $"{refund}";
    _demolishRefundText.color = Color.green;
}
```

---

### [6] GetBuildingIcon() 신규 추가

```csharp
/// <summary>
/// _buildingUpgradeIcons 리스트에서 해당 BuildingType의 아이콘 Sprite를 조회한다.
/// 매핑이 없으면 null을 반환한다.
/// </summary>
private Sprite GetBuildingIcon(BuildingType type)
{
    if (_buildingUpgradeIcons == null) return null;
    foreach (var entry in _buildingUpgradeIcons)
    {
        if (entry.buildingType == type) return entry.icon;
    }
    return null;
}
```

---

### [7] OnDemolishButtonClick() 신규 추가

```csharp
/// <summary>
/// 철거 버튼 클릭 핸들러.
/// 실제 철거 로직은 별도 작업 예정이므로 현재는 로그만 출력한다.
/// </summary>
private void OnDemolishButtonClick()
{
    // TODO: 철거 로직 구현 예정 (별도 작업)
    Debug.Log($"[ProductionPanelUI] 철거 버튼 클릭 — BuildingType: {_currentBarracks?.Type}, 로직 미구현");
}
```

---

## Inspector 재구성 (Game.unity 씬)

### _unitButtons / 관련 리스트를 3개로 줄이기
현재 6개인 아래 필드에서 인덱스 3~5에 해당하는 항목을 제거:
- `_unitButtons`
- `_unitButtonPortraits`
- `_unitCostTexts`
- `_unitAutoIndicators`
- `_unitLockIndicators`

### 액션 버튼 재배선
| 필드 | 연결할 버튼 |
|------|------------|
| `_rallyPointButton` | 그리드 하단 좌 버튼 |
| `_upgradeButton` | 그리드 하단 중 버튼 |
| `_demolishButton` | 그리드 하단 우 버튼 (신규) |

### 신규 필드 연결
| 필드 | 연결 대상 |
|------|----------|
| `_upgradeIconImage` | 업그레이드 버튼 내 아이콘 Image |
| `_upgradeButtonGroup` | 업그레이드 버튼 GO의 CanvasGroup |
| `_rallyGoldDisplay` | 랠리 버튼 내 골드 영역 부모 GO |
| `_demolishRefundText` | 철거 버튼 내 비용 텍스트 TMP |
| `_buildingUpgradeIcons` | 각 BuildingType별 아이콘 Sprite 매핑 |

### 잠금 인디케이터 아이콘 확인
- `_unitLockIndicators[0~2]` 각 GO에 `ui_icon_lock.png` Sprite가 이미지로 설정되어 있는지 확인.
- 설정되어 있지 않다면 Inspector에서 해당 GO의 Image 컴포넌트에 `ui_icon_lock`을 연결.

---

## 위험 요소

| 위험 | 설명 | 대응 |
|------|------|------|
| CanvasGroup 미부착 | 업그레이드 버튼 GO에 CanvasGroup이 없으면 NPE | Initialize에서 null 체크 |
| Inspector 매핑 누락 | `_buildingUpgradeIcons`에 모든 생산건물 매핑이 없으면 아이콘 미표시 | null 아이콘은 조용히 무시 |
| 기존 SetActive 코드 잔존 | `_upgradeButton.gameObject.SetActive` 잔존 시 레이아웃 이동 재발 | Plan [4]에서 완전 제거 확인 |

---

## Inspector 재구성 — 1회성 에디터 스크립트

Inspector 재구성도 수동 작업 대신 에디터 스크립트로 자동화한다.
메뉴: `Hexiege/Setup/ProductionPopup UI 재구성`
파일: `Assets/_Project/Scripts/Editor/SetupProductionPopupUI.cs`

### 에디터 스크립트가 처리할 항목

| 항목 | 처리 방식 |
|------|-----------|
| `_unitButtons` 6→3 | SerializedProperty.DeleteArrayElementAtIndex (인덱스 3~5 제거) |
| `_unitButtonPortraits` 6→3 | 동일 |
| `_unitCostTexts` 6→3 | 동일 |
| `_unitAutoIndicators` 6→3 | 동일 |
| `_unitLockIndicators` 6→3 | 동일 |

> **신규 필드 연결** (`_demolishButton`, `_upgradeIconImage`, `_upgradeButtonGroup`, `_rallyGoldDisplay`, `_demolishRefundText`, `_buildingUpgradeIcons`)은
> 씬 계층구조의 정확한 GO 이름을 에디터 스크립트에서 알 수 없으므로
> 스크립트 실행 후 사용자가 Inspector에서 수동 연결한다.

### 스크립트 구조
```
[MenuItem("Hexiege/Setup/ProductionPopup UI 재구성")]
static void Setup()
{
    1. FindFirstObjectByType<ProductionPanelUI>() → null이면 에러 출력 후 return
    2. SerializedObject로 래핑
    3. 각 리스트 프로퍼티에서 인덱스 3~5 역순 삭제 (5→4→3 순서)
    4. serializedObject.ApplyModifiedProperties()
    5. EditorUtility.SetDirty() + 씬 저장 안내 로그
}
```

---

## 작업 순서

```
[1] ProductionPanelUI.cs 코드 수정 (game-programmer 에이전트)
      ↓
[2] SetupProductionPopupUI.cs 에디터 스크립트 작성 (game-programmer 에이전트)
      ↓
[3] 사용자: 메뉴 Hexiege/Setup/ProductionPopup UI 재구성 실행
      ↓
[4] 사용자: 신규 필드 Inspector 수동 연결
      (_demolishButton, _upgradeIconImage, _upgradeButtonGroup,
       _rallyGoldDisplay, _demolishRefundText, _buildingUpgradeIcons)
      ↓
[5] 사용자 플레이 테스트
```

---

## 추가 작업 — 업그레이드 아이콘 팀 색상 분리 (2026-05-18)

### 자연어 설명

업그레이드 버튼 아이콘에 표시되는 건물 Sprite를 **블루팀/레드팀에 맞는 색상**으로 표시하도록 수정한다.  
기존에는 Sprite 하나(`icon`)만 연결하는 구조였으나, `blueIcon` / `redIcon` 두 필드로 분리하고  
런타임에 현재 팀에 맞는 Sprite를 선택해 업그레이드 버튼에 표시한다.  
에디터 스크립트도 두 Sprite를 자동으로 찾아 연결하도록 확장한다.

---

### Sprite 파일 명명 규칙

경로: `Assets/_Project/Sprites/Buildings/`  
규칙: `bld_{buildingtype_소문자}_blue.png` / `bld_{buildingtype_소문자}_red.png`

예시:
- `WarAcademy` → `bld_waracademy_blue.png`, `bld_waracademy_red.png`
- `HumanBarracks` → `bld_humanbarracks_blue.png`, `bld_humanbarracks_red.png`
- `InfernoCore` → `bld_infernocore_blue.png`, `bld_infernocore_red.png`

---

### [A] ProductionPanelUI.cs — BuildingIconEntry 구조체 수정

**기존:**
```csharp
[System.Serializable]
public struct BuildingIconEntry
{
    public BuildingType buildingType;
    public Sprite icon;
}
```

**변경 후:**
```csharp
[System.Serializable]
public struct BuildingIconEntry
{
    [Tooltip("대상 건물 타입.")]
    public BuildingType buildingType;
    [Tooltip("Blue 팀 업그레이드 버튼에 표시될 건물 아이콘 Sprite.")]
    public Sprite blueIcon;
    [Tooltip("Red 팀 업그레이드 버튼에 표시될 건물 아이콘 Sprite.")]
    public Sprite redIcon;
}
```

---

### [B] ProductionPanelUI.cs — GetBuildingIcon() 시그니처 변경

**기존:**
```csharp
private Sprite GetBuildingIcon(BuildingType type)
{
    if (_buildingUpgradeIcons == null) return null;
    foreach (var entry in _buildingUpgradeIcons)
    {
        if (entry.buildingType == type) return entry.icon;
    }
    return null;
}
```

**변경 후:**
```csharp
private Sprite GetBuildingIcon(BuildingType type, TeamId team)
{
    if (_buildingUpgradeIcons == null) return null;
    foreach (var entry in _buildingUpgradeIcons)
    {
        if (entry.buildingType == type)
            return team == TeamId.Blue ? entry.blueIcon : entry.redIcon;
    }
    return null;
}
```

---

### [C] ProductionPanelUI.cs — UpdateUpgradeButton() 내 GetBuildingIcon 호출 수정

**기존:**
```csharp
Sprite icon = GetBuildingIcon(nextType.Value);
```

**변경 후:**
```csharp
Sprite icon = GetBuildingIcon(nextType.Value, _currentBarracks.Team);
```

---

### [D] SetupProductionPopupUI.cs — 스텝 [4] 추가: _buildingUpgradeIcons 자동 채우기

업그레이드 대상 BuildingType 16개를 하드코딩하고,  
`AssetDatabase.FindAssets()`로 각 타입의 blue/red Sprite를 탐색해  
`_buildingUpgradeIcons` 리스트에 자동으로 채운다.

**업그레이드 대상 목록 (16개):**

| BuildingType | blue 파일명 | red 파일명 |
|---|---|---|
| WarAcademy | bld_waracademy_blue | bld_waracademy_red |
| HumanBarracks | bld_humanbarracks_blue | bld_humanbarracks_red |
| Armory | bld_armory_blue | bld_armory_red |
| WeaponForge | bld_weaponforge_blue | bld_weaponforge_red |
| VehicleBay | bld_vehiclebay_blue | bld_vehiclebay_red |
| BlazeConduit | bld_blazeconduit_blue | bld_blazeconduit_red |
| InfernoCore | bld_infernocore_blue | bld_infernocore_red |
| TidalNexus | bld_tidalnexus_blue | bld_tidalnexus_red |
| OceanicHeart | bld_oceanicheart_blue | bld_oceanicheart_red |
| TerraForge | bld_terraforge_blue | bld_terraforge_red |
| GaeaSanctum | bld_gaeasanctum_blue | bld_gaeasanctum_red |
| PrimalDen | bld_primalden_blue | bld_primalden_red |
| PrimalSanctuary | bld_primalsanctuary_blue | bld_primalsanctuary_red |
| FeralDen | bld_feralden_blue | bld_feralden_red |
| FeralSanctuary | bld_feralsanctuary_blue | bld_feralsanctuary_red |
| FloralNursery | bld_floralnursery_blue | bld_floralnursery_red |

**스크립트 처리 방식:**
```
1. so.FindProperty("_buildingUpgradeIcons") 로 리스트 프로퍼티 취득
2. 이미 16개 이상이면 "이미 채워진 상태"로 건너뜀
3. 16개 BuildingType 순회:
   a. "bld_{type_소문자}_blue t:Sprite" 로 AssetDatabase.FindAssets 탐색
   b. "bld_{type_소문자}_red t:Sprite" 로 탐색
   c. 리스트에 InsertArrayElementAtIndex → 구조체 필드 설정
4. Sprite를 찾지 못한 항목은 LogWarning 출력 후 진행 (null 허용)
```

---

### 위험 요소

| 위험 | 대응 |
|---|---|
| Inspector에서 기존 `icon` 필드 데이터 소실 | 구조체 필드명이 바뀌므로 기존 데이터는 자동 이전 불가 → 에디터 스크립트 [4]가 재채우기 |
| Sprite 파일명이 규칙과 다른 경우 | FindAssets 실패 → LogWarning 출력, null 아이콘은 조용히 무시 |

---

### 추가 작업 순서

```
[A~C] ProductionPanelUI.cs 코드 수정 (game-programmer 에이전트)
      ↓
[D] SetupProductionPopupUI.cs 스텝 [4] 추가 (game-programmer 에이전트)
      ↓
[E] 사용자: 메뉴 Hexiege/Setup/ProductionPopup UI 재구성 재실행
      (스텝 [4]가 _buildingUpgradeIcons 자동 채우기)
      ↓
[F] 사용자: Ctrl+S 씬 저장 후 플레이 테스트
```

---

## 추가 작업 — 테스트 피드백 반영 (2026-05-18)

### 자연어 설명

플레이 테스트에서 발견된 3가지 동적 로직 문제를 수정한다.

1. **철거 환불 금액**: 현재 건물의 건설비만 기준으로 계산하던 것을, 1단계부터 현재 단계까지 투자한 모든 비용(건설비 + 업그레이드비 합산)의 50%로 수정한다.
2. **유닛 2종류 레이아웃**: 유닛이 2개뿐인 건물에서 버튼이 왼쪽으로 붙는 현상을 수정한다. 가운데 슬롯을 CanvasGroup으로 숨겨서 `[유닛1][빈슬롯][유닛2]` 형태로 표시한다.
3. **건물 이름 표시**: 팝업 상단의 HeaderText에 현재 열려있는 건물 이름을 동적으로 표시한다. 이름은 `BuildingType.ToString()`에서 추출한다 (예: `BuildingType.Garage` → `"Garage"`).

---

### [E] 철거 환불 누적 계산 — 설계 방식

**문제**: 현재는 `BuildingStats.GetGoldCost(현재 BuildingType, race) / 2`만 계산한다.
2/3단계 건물은 이전 단계 업그레이드 비용도 투자했으므로 정확하지 않다.

**변경 후 계산 방식:**
```
환불 = (1단계 건설비 + 1→2 업그레이드비 + 2→3 업그레이드비 ...) / 2
```

**설계: GameBootstrapper 초기화 시 1회 계산 → BuildingStats 캐싱**
- 팝업이 열릴 때마다 체인을 탐색하지 않고, 게임 시작 시 1회만 계산해 저장한다.
- 이후 팝업은 `BuildingStats.GetTotalInvestedCost(type, race)`를 단순 조회만 한다.
- Config 에셋에 별도 필드를 추가할 필요 없음. 기존 `GetGoldCost` / `GetUpgradeCost` 데이터만 사용.

**변경 파일 3개:**

#### (1) BuildingStats.cs — 캐시 딕셔너리 + 조회 메서드 추가

```csharp
// (BuildingType, RaceId) → 해당 건물까지 투자된 총 골드 (건설비 + 업그레이드비 합산)
private static readonly Dictionary<(BuildingType, RaceId), int> _totalInvestedCostCache
    = new Dictionary<(BuildingType, RaceId), int>();

// 초기화: GameBootstrapper에서 InitializeBuildingStatsFromConfig 완료 후 호출
public static void SetTotalInvestedCost(BuildingType type, RaceId race, int total)
    => _totalInvestedCostCache[(type, race)] = total;

// 조회: ProductionPanelUI.UpdateDemolishRefund에서 사용
public static int GetTotalInvestedCost(BuildingType type, RaceId race)
    => _totalInvestedCostCache.TryGetValue((type, race), out int v) ? v : GetGoldCost(type, race);
```

#### (2) GameBootstrapper.cs — InitializeBuildingStatsFromConfig() 완료 후 계산 로직 추가

```
모든 종족(Human/Spirit/Transcendence)에 대해:
  BuildingTypeHelper의 모든 생산건물을 GetStage==1인 것만 찾아 순방향 체인 순회:
    stage1: totalCost = GetGoldCost(stage1, race)
    stage2: totalCost = stage1.totalCost + GetUpgradeCost(stage1)
    stage3: totalCost = stage2.totalCost + GetUpgradeCost(stage2)
  각 단계에서 BuildingStats.SetTotalInvestedCost(type, race, totalCost) 호출
```

예시:
```
TrainingCamp(1단계): SetTotalInvestedCost(TrainingCamp, Human, GetGoldCost(TrainingCamp, Human))
WarAcademy(2단계):   SetTotalInvestedCost(WarAcademy, Human,
                       GetGoldCost(TrainingCamp, Human) + GetUpgradeCost(TrainingCamp))
HumanBarracks(3단계): SetTotalInvestedCost(HumanBarracks, Human,
                       WarAcademy의 totalCost + GetUpgradeCost(WarAcademy))
```

비생산 건물(Castle, MiningPost 등)은 계산 대상 제외.

#### (3) ProductionPanelUI.cs — UpdateDemolishRefund() 수정

**기존:**
```csharp
int buildCost = BuildingStats.GetGoldCost(_currentBarracks.Type, race);
int refund = buildCost / 2;
```

**변경 후:**
```csharp
// GameBootstrapper 초기화 시 캐싱된 누적 투자 비용을 직접 조회한다.
int totalInvested = BuildingStats.GetTotalInvestedCost(_currentBarracks.Type, race);
int refund = totalInvested / 2;
```

---

### [F] ProductionPanelUI.cs — BindButtonUnitTypes() 수정 (유닛 2종류 레이아웃)

**문제**: 현재는 `_unitButtons[i].gameObject.SetActive(hasUnit)`으로 처리한다.
유닛이 2개일 때 SetActive(false)로 슬롯 3이 사라지고 버튼 2개가 왼쪽으로 붙는다.

**변경 후:**
- 유닛 3개: `[유닛1][유닛2][유닛3]` — 기존과 동일
- 유닛 2개: `[유닛1][CanvasGroup α=0][유닛2]` — 가운데 슬롯 숨김 (레이아웃 유지)
- 유닛 1개: `[유닛1][CanvasGroup α=0][CanvasGroup α=0]` — 나머지 숨김

업그레이드 버튼과 동일한 패턴:
```csharp
// CanvasGroup이 없는 버튼 GO에는 AddComponent로 추가
// alpha=0, blocksRaycasts=false → 보이지 않고 클릭도 안 됨
// alpha=1, blocksRaycasts=true  → 정상 표시
```

**슬롯 배치 규칙 (유닛 2개 시):**
```
슬롯 0 → activeUnitTypes[0]  (정상 표시)
슬롯 1 → 없음               (CanvasGroup α=0)
슬롯 2 → activeUnitTypes[1]  (정상 표시)
```

**Inspector 필드 추가:**
```csharp
[Header("Unit Button Groups")]
[Tooltip("각 유닛 버튼 GO에 부착된 CanvasGroup. 유닛이 없는 슬롯을 숨길 때 alpha=0으로 처리한다.")]
[SerializeField] private List<CanvasGroup> _unitButtonGroups;
```

---

### [G] ProductionPanelUI.cs — HeaderText 건물 이름 동적 표시

**목적**: 팝업 상단의 HeaderText(TextMeshProUGUI)에 현재 열려있는 건물 이름을 표시한다.

**이름 추출 방식**: `BuildingType.ToString()`
- `BuildingType.Garage` → `"Garage"`
- `BuildingType.TrainingCamp` → `"TrainingCamp"`
- `BuildingType.HumanBarracks` → `"HumanBarracks"`

**Inspector 필드 추가:**
```csharp
[Header("Header")]
[Tooltip("팝업 상단에 건물 이름을 표시하는 텍스트. Show() 호출 시 BuildingType.ToString()으로 갱신된다.")]
[SerializeField] private TextMeshProUGUI _headerText;
```

**Show() 내 추가 코드:**
```csharp
if (_headerText != null)
    _headerText.text = barracks.Type.ToString();
```

---

### 파일별 변경 요약

| 파일 | 변경 항목 |
|------|-----------|
| `ProductionPanelUI.cs` | `UpdateDemolishRefund()` 누적 계산, `BindButtonUnitTypes()` 2종 레이아웃, `_headerText` 필드 + Show() 갱신, `_unitButtonGroups` 필드 추가 |

---

### 추가 작업 순서

```
[E~G] ProductionPanelUI.cs 코드 수정 (game-programmer 에이전트)
      ↓
[H] 사용자: Inspector에서 _headerText, _unitButtonGroups 필드 연결
      ↓
[I] 사용자 플레이 테스트
```

---

## 추가 작업 — 테스트 피드백 2차 (2026-05-18)

### 자연어 설명

테스트 결과 `UpdateButtonPortraits()`가 2유닛 건물의 슬롯 배치를 인식하지 못해
이전 건물의 초상화가 슬롯2에 잔존하는 버그가 발견됐다.

원인: `UpdateButtonPortraits()`는 `list[i]`를 `_unitButtonPortraits[i]`에 그대로 넣는다.
2유닛 건물(list.Count=2)일 때:
- portrait[0] = list[0] ✅
- portrait[1] = list[1] ← 숨겨진 슬롯(가운데)에 잘못 들어감 ❌
- portrait[2] = 갱신 없음 → 이전 건물 초상화 잔존 ❌

### [H] ProductionPanelUI.cs — UpdateButtonPortraits() 수정

2유닛 레이아웃(`list.Count == 2`) 시 슬롯 배치:
- slot0 → list[0] 초상화
- slot1 → 스킵 (CanvasGroup으로 숨겨진 더미 슬롯, 초상화 갱신 불필요)
- slot2 → list[1] 초상화

비2유닛 시 기존 동작 유지 (`portrait[i] = list[i]`).

---

## 추가 작업 — 2/3단계 건물 랠리 마커 미표시 버그 (2026-05-18)

### 자연어 설명

1단계 건물은 랠리 마커가 정상 표시되지만, **업그레이드된 2/3단계 건물은 랠리 포인트를 설정해도 마커가 나타나지 않는다.**

원인은 `ProductionTicker`가 `GameEvents.OnBuildingPlaced`만 구독하고 있어서,  
건물이 업그레이드될 때 발생하는 `GameEvents.OnBuildingUpgraded` 이벤트를 처리하지 못하기 때문이다.

- 1단계 배치 시: `OnBuildingPlaced` → `RegisterBarracks()` → `UnitProductionUseCase._states`에 등록 ✅  
- 업그레이드 시: `OnBuildingUpgraded` 발생, 하지만 `ProductionTicker`가 이를 구독하지 않음 → 새 건물 미등록 ❌  
- 미등록 상태에서 `SetRallyPoint(barracksId)` 호출 → `_states.TryGetValue` 실패 → 즉시 리턴 → 마커 생성 안 됨 ❌

---

### [I] ProductionTicker.cs — OnBuildingUpgraded 구독 추가

**변경 파일:** `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs`

**수정 내용:**

`Initialize()` 내에서 `GameEvents.OnBuildingUpgraded`를 구독한다.  
핸들러에서는:
1. 기존 건물(업그레이드 전 BuildingType)의 ProductionState를 `_useCase`에서 제거
2. 새 건물(업그레이드 후 `IBuilding`)을 `RegisterBarracks()`로 등록

```csharp
// Initialize() 내 구독 추가
GameEvents.OnBuildingUpgraded += HandleBuildingUpgraded;
```

```csharp
// 핸들러
private void HandleBuildingUpgraded(IBuilding newBuilding)
{
    // 업그레이드된 건물만 처리 (생산건물 여부 확인)
    if (!BuildingTypeHelper.IsProductionBuilding(newBuilding.Type)) return;

    // 기존 상태 제거 후 새 건물로 재등록
    // (barracksId는 업그레이드 전후 동일하다 — 같은 Building 인스턴스의 Type만 바뀜)
    _useCase.UnregisterBarracks(newBuilding.Id);
    RegisterBarracks(newBuilding);
}
```

**OnDestroy() 내 구독 해제도 추가:**
```csharp
GameEvents.OnBuildingUpgraded -= HandleBuildingUpgraded;
```

---

### 전제 확인 필요

아래 사항은 구현 전에 코드를 직접 확인해야 한다:

| 확인 항목 | 확인 방법 |
|-----------|-----------|
| `GameEvents.OnBuildingUpgraded` 시그니처 | `GameEvents.cs` 파일에서 델리게이트 타입 확인 |
| `UnitProductionUseCase.UnregisterBarracks()` 존재 여부 | 없으면 직접 `_states.Remove(id)` 또는 신규 메서드 추가 필요 |
| 업그레이드 전후 `IBuilding.Id` 동일 여부 | `BuildingPlacementUseCase.UpgradeBuilding()` 확인 |
| `RegisterBarracks()` 메서드 존재/시그니처 | `ProductionTicker.cs` 내부 확인 |

---

### 예상 변경 파일

| 파일 | 변경 내용 |
|------|-----------|
| `ProductionTicker.cs` | `OnBuildingUpgraded` 구독, `HandleBuildingUpgraded` 핸들러 추가, OnDestroy 해제 |
| `UnitProductionUseCase.cs` | `UnregisterBarracks()` 메서드가 없으면 추가 |

---

### 추가 작업 순서

```
[I] ProductionTicker.cs 수정 (game-programmer 에이전트) ✅ 완료
      ↓
[J] 사용자: 2/3단계 건물 랠리 포인트 플레이 테스트 ✅ 완료 — 전 종족 테스트 통과 (2026-05-18)
```
