# 신규 에셋 게임 반영 계획

## 이 작업이 무엇인가

새로 만든 유닛·건물 프리팹들이 실제 게임에서 동작하려면,
코드가 새 타입을 인식하고 → 스탯을 읽고 → 씬에 올바르게 소환하고 → UI에 이미지를 표시할 수 있어야 합니다.

이 계획서는 그 전체 연결 과정을 순서대로 정리합니다.
**UI 이미지(초상화) 준비와 연결도 포함합니다.**

---

## ⚠️ 구현 시작 전 결정 필요 사항

### 결정 A — 종족당 배럭 생산 유닛 수

현재 `ProductionPanelUI`는 **종족당 3개 유닛**으로 하드코딩되어 있습니다.
신규 유닛(Tank, CannonCart, DustSpirit, BoulderSpirit, QuakeSpirit 등)을 어떻게 제공할지 결정 필요:

| 선택 | 내용 | 영향 |
|------|------|------|
| **A-1**: 배럭 3종 유지, 신규 유닛은 특수 건물에서 생산 | Human 배럭: Pistoleer/Assault/Sniper 유지. Tank는 TechnicalLaboratory, CannonCart는 FlightFacility에서 별도 생산 | ProductionPanelUI 코드 변경 없음. 특수 건물용 생산 UI 별도 제작 필요 |
| **A-2**: 배럭 유닛 확장 (4개 이상) | 스크롤 리스트 또는 탭 추가 | ProductionPanelUI 대규모 수정 필요 |

**→ 이 결정에 따라 이후 작업 범위가 크게 달라집니다. 구현 시작 전 확정 필요.**

### 결정 B — 신규 건물 기능 구현 범위

| 선택 | 내용 |
|------|------|
| **B-1**: 배치(시각적 표시)만 구현 | 새 건물이 타일에 올라오고 피격 가능하지만, 포탑 공격/힐 등 특수 기능은 추후 구현 |
| **B-2**: 기능까지 구현 | AutoTower 자동 공격, HealShrine 힐, FlightFacility 폭격 지원 등 게임 로직 추가 |

---

## 단계별 계획

### 1단계 — UnitType 열거형 확장

**파일**: `Assets/_Project/Scripts/Domain/Unit/UnitType.cs`

현재 9개(0~8) → 신규 9개 추가 (확정 순서는 사용자가 결정):

```csharp
// ── 인간계 신규 ──
Tank             = 9,   // 중장갑 포격 전차
CannonCart       = 10,  // 원거리 포격 수레

// ── 정령계 신규 (흙 원소) ──
DustSpirit       = 11,  // 흙정령 1단계
BoulderSpirit    = 12,  // 흙정령 2단계
QuakeSpirit      = 13,  // 흙정령 3단계

// ── 초월계 신규 ──
RhinoBreaker     = 14,  // 돌진 탱커
EagleArcher      = 15,  // 원거리 궁수
RabbitTrickster  = 16,  // 민첩 근접
MushroomBomber   = 17   // 범위 폭발 딜러
```

---

### 2단계 — BuildingType 열거형 확장

**파일**: `Assets/_Project/Scripts/Domain/Building/BuildingType.cs`

현재 3개(Castle/Barracks/MiningPost) → 신규 타입 추가:

```csharp
AutoTower,       // 자동 방어 포탑 — CannonTower(H), RuneSpire(S), VineTower(T) 공용
FlightFacility,  // 지원 건물 — FlightFacility(H)
Research,        // 업그레이드/연구 건물 — TechnicalLaboratory(H), AstronomicalSpirit(S)
MagicBuilding,   // 마법 건물 — MagicSpirit(S), WillowShrine(T)
HealShrine,      // 힐 건물 — MistShrine(T)
```

> PrimalAltar(T)는 기존 `Barracks` 타입 재사용.
> HunterPlant(T)는 기존 AssetList에 `AncientGrove` 예정이었으나 `Research`로 통합 가능 — 사용자 확인.

---

### 3단계 — UnitStatsConfig 항목 추가

**파일**: `Assets/_Project/Resources/Config/UnitStatsConfig.asset`

1단계 enum 추가 후 Unity Inspector에서 직접 추가:
- `_stats` 리스트에 신규 UnitType 9개 항목 추가
- 각 항목 필드: `unitType`, `maxHp`, `attackPower`, `attackRange`, `detectRange`, `moveSpeed`, `attackCooldown`, `hitFrameTimes[]`, `productionTime`, `goldCost`, `populationCost`, `occupancySize`
- **hitFrameTimes**: 공격 애니메이션에서 실제 히트 발생 시간(초) — 애니메이션 파일에서 측정하여 입력
- 스탯 수치는 기획 확정 후 사용자 직접 입력

또는 `SetupUnitStatsConfig.cs`의 `PopulateDefaults()` 메서드에 신규 항목 추가 후 기존 .asset 삭제 → 메뉴 실행으로 재생성.

---

### 4단계 — BuildingStatsConfig 항목 추가

**파일**: `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`

2단계 enum 추가 후 Unity Inspector에서 직접 추가:
- `_stats` 리스트에 신규 BuildingType 항목 추가
- 각 항목 필드: `buildingType`, `humanMaxHp/spiritMaxHp/transcendenceMaxHp`, `humanGoldCost/spiritGoldCost/transcendenceGoldCost`, `humanAttackPower/spiritAttackPower/transcendenceAttackPower`
- AutoTower 계열은 attackPower 값 필요. 나머지는 0.

---

### 5단계 — BuildingFactory 구조 수정

**파일**: `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`

현재 `BuildingTeamPrefabSet` struct는 `castle`, `barracks`, `miningPost` 3개 필드만 존재.
신규 BuildingType에 대응하는 프리팹 필드 추가 필요:

```csharp
[System.Serializable]
public struct BuildingTeamPrefabSet
{
    public GameObject castle;
    public GameObject barracks;
    public GameObject miningPost;
    // 신규 추가
    public GameObject autoTower;
    public GameObject flightFacility;
    public GameObject research;
    public GameObject magicBuilding;
    public GameObject healShrine;
}
```

그리고 `CreateBuildingObject()` 내부 switch 분기에 신규 케이스 추가:
```csharp
GameObject prefab = data.Type switch
{
    BuildingType.Castle         => set.castle,
    BuildingType.Barracks       => set.barracks,
    BuildingType.MiningPost     => set.miningPost,
    BuildingType.AutoTower      => set.autoTower,        // 신규
    BuildingType.FlightFacility => set.flightFacility,   // 신규
    BuildingType.Research       => set.research,         // 신규
    BuildingType.MagicBuilding  => set.magicBuilding,    // 신규
    BuildingType.HealShrine     => set.healShrine,       // 신규
    _ => null
};
```

struct 변경 후 Inspector에서 종족별 6세트 각각에 신규 프리팹 연결 필요.

---

### 6단계 — UnitFactory Editor 스크립트 수정 + 프리팹 연결

**파일**: `Assets/_Project/Scripts/Editor/SetupUnitFactoryPrefabs.cs`

`_raceMappings` 배열에 신규 유닛 항목 추가:

```csharp
// ── 인간계 ──
new RaceMapping
{
    raceFolderName = "Human",
    units = new[]
    {
        (UnitType.Pistoleer,  "Pistoleer"),
        (UnitType.Assault,    "Assault"),
        (UnitType.Sniper,     "Sniper"),
        (UnitType.Tank,       "Tank"),       // 신규
        (UnitType.CannonCart, "CannonCart")  // 신규
    }
},
// ── 정령계 ──
new RaceMapping
{
    raceFolderName = "Spirit",
    units = new[]
    {
        (UnitType.FlameSpirit,   "FlameSpirit"),
        (UnitType.EmberSpirit,   "EmberSpirit"),
        (UnitType.InfernoSpirit, "InfernoSpirit"),
        (UnitType.DustSpirit,    "DustSpirit"),    // 신규
        (UnitType.BoulderSpirit, "BoulderSpirit"), // 신규
        (UnitType.QuakeSpirit,   "QuakeSpirit"),   // 신규
    }
},
// ── 초월계 ──
new RaceMapping
{
    raceFolderName = "Transcendence",
    units = new[]
    {
        (UnitType.BearGuard,       "BearGuard"),
        (UnitType.FoxMagician,     "FoxMagician"),
        (UnitType.LionKnight,      "LionKnight"),
        (UnitType.RhinoBreaker,    "RhinoBreaker"),    // 신규
        (UnitType.EagleArcher,     "EagleArcher"),     // 신규
        (UnitType.RabbitTrickster, "RabbitTrickster"), // 신규
        (UnitType.MushroomBomber,  "MushroomBomber"),  // 신규
    }
}
```

수정 후 메뉴 `Hexiege/Setup/UnitFactory 프리팹 연결` 실행 → 자동 연결.

---

### 7단계 — UI 초상화 이미지 준비 및 연결

**관련 파일**: `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

ProductionPanelUI Inspector에는 팀(Blue/Red) × 종족(3) = 6세트의 `UnitPortraitSet`이 연결되어 있음.
현재 각 세트: `slot1`, `slot2`, `slot3` 스프라이트 3개 고정.

**7-1. 초상화 이미지 준비**
신규 유닛 초상화 PNG 파일 준비 (1024×1024 권장, CommonAssetGuide 기준):
- Human: Tank, CannonCart
- Spirit: DustSpirit, BoulderSpirit, QuakeSpirit
- Transcendence: RhinoBreaker, EagleArcher, RabbitTrickster, MushroomBomber

**7-2. 결정 A에 따른 분기**

**A-1 선택 시 (배럭 3종 유지)**:
- 현재 `UnitPortraitSet` 구조(slot1~3) 유지
- 배럭에 표시되는 3종 유닛의 초상화는 현재 세트 유지
- 신규 유닛은 특수 건물 생산 UI에서 별도 초상화 연결

**A-2 선택 시 (배럭 4개 이상)**:
- `UnitPortraitSet` struct를 slot4 이상으로 확장
- 버튼 3개 → 동적 리스트로 교체
- `BindButtonUnitTypes()` 메서드 리팩토링

---

### 8단계 — NetworkObject 컴포넌트 확인

멀티플레이 환경에서는 모든 유닛/건물 프리팹에:
- `NetworkObject` 컴포넌트 필수
- 유닛: `NetworkUnit` 컴포넌트 필수

신규 프리팹 전체 점검 필요. 누락 시 에디터에서 컴포넌트 추가.

---

### 9단계 — 건물 배치 UI 반영

**파일**: `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

신규 건물 타입이 게임 내 배치 버튼에 표시되려면 BuildingPlacementUI에 신규 BuildingType 버튼/아이콘 추가 필요.
범위와 방식은 BuildingPlacementUI 코드 확인 후 결정.

---

## 작업 순서 요약

```
[선행] 결정 A·B 확정 (배럭 유닛 수, 신규 건물 기능 범위)
         ↓
[1] UnitType.cs 열거형 추가 (코드)
[2] BuildingType.cs 열거형 추가 (코드)
         ↓
[3] UnitStatsConfig.asset 항목 추가 (Inspector + 스탯 입력)
[4] BuildingStatsConfig.asset 항목 추가 (Inspector + 스탯 입력)
         ↓
[5] BuildingFactory.cs struct·switch 수정 + Inspector 프리팹 연결 (코드+Inspector)
[6] SetupUnitFactoryPrefabs.cs 수정 → 메뉴 실행으로 자동 연결 (코드+에디터)
         ↓
[7] 초상화 이미지 준비 → Inspector UnitPortraitSet 연결 (이미지+Inspector)
         ↓
[8] NetworkObject 컴포넌트 확인 (Inspector)
[9] BuildingPlacementUI 신규 건물 반영 (코드+Inspector)
         ↓
[10] 싱글플레이 동작 테스트 → 멀티플레이 동작 테스트
```

---

## 영향 범위 요약

| 파일 | 변경 내용 | 코드? | Inspector? |
|------|----------|-------|-----------|
| `Domain/Unit/UnitType.cs` | enum 9개 추가 | ✅ | - |
| `Domain/Building/BuildingType.cs` | enum 추가 | ✅ | - |
| `Resources/Config/UnitStatsConfig.asset` | 신규 항목 + 스탯 | - | ✅ |
| `Resources/Config/BuildingStatsConfig.asset` | 신규 항목 + 스탯 | - | ✅ |
| `Infrastructure/Factories/BuildingFactory.cs` | struct·switch 확장 | ✅ | ✅ |
| `Editor/SetupUnitFactoryPrefabs.cs` | `_raceMappings` 확장 | ✅ | ✅(메뉴) |
| `Presentation/UI/ProductionPanelUI.cs` | 결정 A에 따라 | 조건부 | 조건부 |
| `Presentation/UI/BuildingPlacementUI.cs` | 신규 건물 버튼 | ✅ | ✅ |
| 신규 유닛/건물 프리팹 | NetworkObject 확인 | - | ✅ |
| UI 초상화 이미지 | 이미지 파일 + 연결 | - | ✅ |
