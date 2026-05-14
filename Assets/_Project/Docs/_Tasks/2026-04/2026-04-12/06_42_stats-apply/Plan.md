# Plan: 유닛/건물 스탯 적용 및 UI 표기

**작업일:** 2026-04-12  
**기준 문서:** `Assets/_Project/Docs/StatsReference.md`

---

## 구현 목표

1. `UnitStats.cs` — 플레이스홀더 스탯을 StatsReference.md 기준값으로 교체
2. `UnitProductionStats.cs` — 생산 시간/골드 비용 수정
3. `BuildingStats.cs` — 종족별 HP 지원 추가 (Transcendence 빌딩 HP 차이 처리)
4. `BuildingPlacementUseCase.cs` — BuildingStats 호출부 수정 (RaceId 전달)
5. `ProductionPanelUI.cs` — 유닛별 골드 비용/스탯 텍스트 표기 추가
6. `BuildingPlacementUI.cs` — 건물별 HP/비용 텍스트 표기 추가

**다히트 공격 범위 외 처리:**
- LionKnight 주석을 "4히트" → "2히트"로 수정
- 이번 구현에서는 첫 타격 1히트만 적용 (현행 유지)
- 다중 히트 구현은 별도 작업으로 분리

---

## 아키텍처 제약

- Domain 레이어 → Core 레이어 참조 금지
- `BuildingPlacementUseCase` (Application) → `GameRaceContext` (Infrastructure) 직접 참조 금지
  - **해결 방법:** `PlaceBuilding()`/`PlaceBuildingWithId()` 메서드에 `RaceId` 파라미터 추가.
    호출자(BuildingPlacementUI = Presentation, NetworkBuildingController = Infrastructure)가
    `GameRaceContext`에서 조회 후 전달.

---

## 파일별 구현 계획

### [1] UnitStats.cs
**경로:** `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`

변경 항목:

| 유닛 | 항목 | 변경 전 | 변경 후 |
|------|------|---------|---------|
| Pistoleer | 이동속도 | 1.0f | 0.5f |
| FlameSpirit | HP | 30 | 50 |
| FlameSpirit | 공격력 | 5 | 2 |
| InfernoSpirit | HP | 30 | 100 |
| InfernoSpirit | 공격력 | 5 | 25 |
| BearGuard | HP | 30 | 200 |
| BearGuard | 공격력 | 5 | 10 |
| FoxMagician | HP | 30 | 20 |
| FoxMagician | 공격력 | 5 | 8 |
| LionKnight | HP | 30 | 50 |
| LionKnight | 공격력 | 5 | 9 |
| LionKnight | HitFrameTime 주석 | "4히트" | "2히트" |

파일 상단 주석 수정:
- "Spirit/Transcendence의 HP/공격력/생산비용은 미정 → 플레이스홀더 값." 제거

---

### [2] UnitProductionStats.cs
**경로:** `Assets/_Project/Scripts/Domain/Unit/UnitProductionStats.cs`

변경 항목:

| 유닛 | 항목 | 변경 전 | 변경 후 |
|------|------|---------|---------|
| EmberSpirit | 생산 시간 | 10f | 5f |
| EmberSpirit | 골드 비용 | 100 | 50 |
| FlameSpirit | 생산 시간 | 5f | 15f |
| FlameSpirit | 골드 비용 | 50 | 200 |
| InfernoSpirit | 생산 시간 | 15f | 30f |
| InfernoSpirit | 골드 비용 | 200 | 500 |
| BearGuard | 생산 시간 | 5f | 25f |
| BearGuard | 골드 비용 | 50 | 400 |
| FoxMagician | 생산 시간 | 10f | 5f |
| FoxMagician | 골드 비용 | 100 | 50 |
| LionKnight | 생산 시간 | 15f | 15f (변경 없음) |
| LionKnight | 골드 비용 | 200 | 200 (변경 없음) |

파일 상단 주석 수정:
- "(미정 → 플레이스홀더)" 문구 제거

---

### [3] BuildingStats.cs
**경로:** `Assets/_Project/Scripts/Domain/Building/BuildingStats.cs`

**변경 방식:** `GetMaxHp(BuildingType, RaceId)` 오버로드 추가.
기존 `GetMaxHp(BuildingType)` 시그니처 유지 (하위 호환, 기본값 `RaceId.Human`).

```csharp
// 기존 메서드 — RaceId 없을 때 Human 기준값 반환 (하위 호환)
public static int GetMaxHp(BuildingType type)
    => GetMaxHp(type, RaceId.Human);

// 신규 메서드 — 종족별 HP 반환
public static int GetMaxHp(BuildingType type, RaceId race) => (type, race) switch
{
    // Castle
    (BuildingType.Castle, RaceId.Transcendence) => 200,
    (BuildingType.Castle, _)                    => 100,
    // Barracks (SummoningAltar, FungalNode 포함)
    (BuildingType.Barracks, RaceId.Transcendence) => 50,
    (BuildingType.Barracks, _)                    => 30,
    // MiningPost (ManaRift, HunterPlant 포함)
    (BuildingType.MiningPost, RaceId.Transcendence) => 40,
    (BuildingType.MiningPost, _)                    => 20,
    _ => 10
};
```

---

### [4] BuildingPlacementUseCase.cs
**경로:** `Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs`

변경 항목:
1. `PlaceBuildingInternal(BuildingType, TeamId, HexCoord, HexTile)` → `RaceId race` 파라미터 추가
2. `PlaceBuildingWithId(int, BuildingType, TeamId, HexCoord)` → `RaceId race` 파라미터 추가
3. `PlaceBuilding(BuildingType, TeamId, HexCoord)` (public 메서드) → `RaceId race` 파라미터 추가
4. 각 메서드 내부: `BuildingStats.GetMaxHp(type)` → `BuildingStats.GetMaxHp(type, race)` 변경

**호출부 변경 (BuildingPlacementUI.cs, NetworkBuildingController.cs):**
- `GameRaceContext` 조회 후 `race` 인자 추가 전달

---

### [5] BuildingPlacementUI.cs — UI 표기 추가
**경로:** `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

추가할 SerializedField:
```csharp
[Header("Building Stats Text")]
[Tooltip("배럭 HP 텍스트")]
[SerializeField] private TextMeshProUGUI _barracksHpText;

[Tooltip("배럭 건설 비용 텍스트")]
[SerializeField] private TextMeshProUGUI _barracksCostText;

[Tooltip("채굴소 HP 텍스트")]
[SerializeField] private TextMeshProUGUI _miningPostHpText;

[Tooltip("채굴소 건설 비용 텍스트")]
[SerializeField] private TextMeshProUGUI _miningPostCostText;
```

`Show()` 내에서 호출할 `UpdateBuildingStats(TeamId team)` 메서드 추가:
- 팀 → 종족 조회 → `BuildingStats.GetMaxHp(type, race)` 호출 → 텍스트 설정
- `_config`에서 건설 비용 조회

**에디터 작업 필요:**
- BuildingPlacementPanel 프리팹에 각 버튼마다 HPText, CostText GameObject 추가
- Inspector에서 새 필드 연결

---

### [6] ProductionPanelUI.cs — UI 표기 추가
**경로:** `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

추가할 SerializedField:
```csharp
[Header("Unit Cost Texts")]
[Tooltip("1번 슬롯 유닛 골드 비용 텍스트")]
[SerializeField] private TextMeshProUGUI _slot1CostText;

[Tooltip("2번 슬롯 유닛 골드 비용 텍스트")]
[SerializeField] private TextMeshProUGUI _slot2CostText;

[Tooltip("3번 슬롯 유닛 골드 비용 텍스트")]
[SerializeField] private TextMeshProUGUI _slot3CostText;
```

`UpdateUI()` 또는 `BindButtonUnitTypes()` 호출 시 비용 텍스트 갱신:
```csharp
// 현재 바인딩된 유닛 타입의 골드 비용을 텍스트로 표시
_slot1CostText?.SetText($"{UnitProductionStats.GetGoldCost(_buttonUnitTypes[0])}G");
_slot2CostText?.SetText($"{UnitProductionStats.GetGoldCost(_buttonUnitTypes[1])}G");
_slot3CostText?.SetText($"{UnitProductionStats.GetGoldCost(_buttonUnitTypes[2])}G");
```

> **UI 표기 범위 확인 필요:**
> 사용자가 원하는 스탯 표기 항목 확인 필요. 현재 계획:
> - 각 유닛 버튼에 **골드 비용** 텍스트만 추가 (CostText)
> - HP / 공격력 / 사거리 등 추가 스탯 표기 여부는 사용자 확인 후 범위 결정

**에디터 작업 필요:**
- 유닛 버튼 각각에 CostText GameObject 추가
- Inspector에서 새 필드 연결

---

## 작업 순서

```
[1] UnitStats.cs 스탯 값 수정
[2] UnitProductionStats.cs 생산 시간/비용 수정
[3] BuildingStats.cs 종족별 오버로드 추가
[4] BuildingPlacementUseCase.cs RaceId 파라미터 추가 + 호출부 수정
[5] BuildingPlacementUI.cs 스탯 텍스트 필드 추가
[6] ProductionPanelUI.cs 비용 텍스트 필드 추가
[7] 에디터 작업 — 프리팹에 텍스트 오브젝트 추가 및 Inspector 연결
```

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| 아키텍처 위반 | Application → Infrastructure 직접 참조 | RaceId 파라미터 전달 방식으로 회피 |
| BuildingPlacementUseCase 호출부 누락 | NetworkBuildingController 등 다수 호출부 | 컴파일 오류로 자동 감지 가능 |
| Spirit/Transcendence 유닛 밸런스 | InfernoSpirit HP 100/공격력 25가 너무 강할 수 있음 | StatsReference.md 기준 적용 후 테스트 확인 |
| ProductionPanelUI 구조 변경 | 기존 버튼 레이아웃에 텍스트 추가 시 레이아웃 깨짐 가능 | 에디터에서 LayoutGroup 설정 확인 |
| LionKnight AttackCooldown 주석 | 현재 코드: "2:20 = 2.33초 (4히트 공격)" → 2히트로 수정 필요 | 단순 주석 수정 |

---

## 다히트 공격 — 별도 작업 기록

| 유닛 | 히트 수 | 타격 프레임 시간 |
|------|---------|----------------|
| FlameSpirit | 6히트 | 0:20, 1:05, 1:13, 1:20, 1:28, 2:03 |
| LionKnight | 2히트 | 0:22, 1:08 |

- 이번 작업: 첫 타격(0:20 / 0:22) 시점에 1회 데미지 적용 (현행 유지)
- 향후 별도 작업에서 각 타격 프레임에 독립 데미지 적용 구현 예정
