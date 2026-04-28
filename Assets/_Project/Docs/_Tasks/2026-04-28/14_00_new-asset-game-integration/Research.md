# 신규 에셋 게임 반영 — Research

## 이 문서의 목적

새로 만든 유닛·건물 프리팹들을 실제 게임 코드에 연결하기 전에,
현재 코드가 어떻게 동작하는지 파악하고, 어느 파일을 어떻게 수정해야 하는지 분석합니다.

---

## 1. 현재 코드 구조 파악

### 1-1. UnitType 열거형 (UnitType.cs)

**파일**: `Assets/_Project/Scripts/Domain/Unit/UnitType.cs`

현재 정의된 값 9개:
```
Pistoleer=0, Assault=1, Sniper=2          // 인간계 3종
FlameSpirit=3, EmberSpirit=4, InfernoSpirit=5  // 정령계 (불) 3종
BearGuard=6, FoxMagician=7, LionKnight=8  // 초월계 3종
```

**필요한 변경**: 신규 유닛 9종(Tank, CannonCart, DustSpirit, BoulderSpirit, QuakeSpirit, RhinoBreaker, EagleArcher, RabbitTrickster, MushroomBomber) enum 값 추가.

---

### 1-2. BuildingType 열거형 (BuildingType.cs)

**파일**: `Assets/_Project/Scripts/Domain/Building/BuildingType.cs`

현재 정의된 값 3개:
```
Castle, Barracks, MiningPost
```

**필요한 변경**: 신규 건물 타입 추가 (AutoTower, FlightFacility, Research, MagicBuilding, HealShrine 등).

---

### 1-3. UnitFactory 프리팹 연결 구조

**파일**: `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`

- 구조: 종족별 `List<UnitPrefabEntry>` 3개 (humanPrefabs, spiritPrefabs, transcendencePrefabs)
- 각 Entry: `type(UnitType)` + `blue(GameObject)` + `red(GameObject)`
- 유닛 소환 시 종족 + UnitType + 팀으로 프리팹을 선형 탐색하여 Instantiate
- 리스트 크기 제한 없음 — 항목 추가만 하면 됨

**자동 연결 스크립트**: `Assets/_Project/Scripts/Editor/SetupUnitFactoryPrefabs.cs`
- 메뉴: `Hexiege/Setup/UnitFactory 프리팹 연결`
- 동작: `_raceMappings` 배열에 정의된 (UnitType, 프리팹 이름) 쌍을 읽어 자동 연결
- 현재 각 종족 3개 유닛만 등록됨 → 신규 유닛 추가 시 `_raceMappings`에 항목 추가 필요

**결론**: ⭕ UnitFactory는 항목 추가만으로 확장 가능 (구조 변경 불필요)

---

### 1-4. BuildingFactory 프리팹 연결 구조 ⚠️

**파일**: `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`

- 구조: `BuildingTeamPrefabSet` struct 6개 (Human/Spirit/Transcendence × Blue/Red)
- struct 필드: `castle`, `barracks`, `miningPost` (고정 3개)
- 건물 소환 시 BuildingType으로 switch 분기:
  ```csharp
  GameObject prefab = data.Type switch
  {
      BuildingType.Castle     => set.castle,
      BuildingType.Barracks   => set.barracks,
      BuildingType.MiningPost => set.miningPost,
      _ => null   // 등록되지 않은 타입은 null 반환 → 오류 로그
  };
  ```

**결론**: ⚠️ 신규 BuildingType 추가 시 struct 필드 확장 + switch 분기 추가 필요
- 이는 코드 구조 수정이 필요한 작업 (UnitFactory보다 복잡)
- 단, 건물 배치 Use Case / 도메인 로직은 수정 없이 새 타입만 추가하면 됨

---

### 1-5. UnitStatsConfig 구조

**파일**: `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
**스크립트**: `Assets/_Project/Scripts/Infrastructure/Config/UnitStatsConfig.cs`
**에디터 생성 스크립트**: `Assets/_Project/Scripts/Editor/SetupUnitStatsConfig.cs`

- 현재 9개 UnitType에 대해 각각 스탯 항목 보유
- 항목당 필드: `unitType`, `maxHp`, `attackPower`, `attackRange`, `detectRange`, `moveSpeed`, `attackCooldown`, `hitFrameTimes[]`, `productionTime`, `goldCost`, `populationCost`, `occupancySize`
- SetupUnitStatsConfig는 기존 .asset이 있으면 덮어쓰지 않음 → 신규 항목은 Inspector에서 직접 추가하거나 스크립트 수정 후 삭제 재생성

**hitFrameTimes 중요**: 공격 애니메이션에서 히트 판정이 발생하는 시간(초). 애니메이션별로 직접 측정하여 입력해야 함.

**결론**: ⭕ Inspector에서 직접 새 항목 추가 가능. 스탯 수치는 게임 기획 확정 후 입력.

---

### 1-6. BuildingStatsConfig 구조

**파일**: `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`
**에디터 생성 스크립트**: `Assets/_Project/Scripts/Editor/SetupBuildingStatsConfig.cs`

- 현재 3개 BuildingType(Castle/Barracks/MiningPost)에 대해 종족별 스탯 보유
- 항목당 필드: `buildingType`, `humanMaxHp/spiritMaxHp/transcendenceMaxHp`, `humanGoldCost/spiritGoldCost/transcendenceGoldCost`, `humanAttackPower/spiritAttackPower/transcendenceAttackPower`
- 신규 BuildingType enum 추가 후 Inspector에서 항목 추가 가능

**결론**: ⭕ Inspector에서 직접 새 항목 추가 가능.

---

### 1-7. ProductionPanelUI 구조 ⚠️

**파일**: `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

현재 UI는 **종족당 정확히 3개 유닛**으로 하드코딩:
- 버튼: `_pistoleerButton`, `_assaultButton`, `_sniperButton` (3개 고정)
- 버튼 초상화: `_pistoleerButtonPortrait`, `_assaultButtonPortrait`, `_sniperButtonPortrait`
- 자동 인디케이터: `_pistoleerAutoIndicator`, `_assaultAutoIndicator`, `_sniperAutoIndicator`
- `UnitPortraitSet` struct: `slot1`, `slot2`, `slot3` 스프라이트 3개 고정
- `BindButtonUnitTypes()`: 종족별로 3개 UnitType 하드코딩
  ```csharp
  case RaceId.Human:
      _buttonUnitTypes[0] = UnitType.Pistoleer;
      _buttonUnitTypes[1] = UnitType.Assault;
      _buttonUnitTypes[2] = UnitType.Sniper;
  case RaceId.Spirit:
      _buttonUnitTypes[0] = UnitType.EmberSpirit;
      _buttonUnitTypes[1] = UnitType.FlameSpirit;
      _buttonUnitTypes[2] = UnitType.InfernoSpirit;
  ```
- 초상화 스프라이트 세트: 팀(Blue/Red) × 종족(3) = 6 `UnitPortraitSet` Inspector 필드

**결론**: ⚠️ 종족당 4개 이상 유닛을 배럭에서 생산하려면 UI 구조 변경 필요.
종족당 3개를 유지(신규 유닛은 다른 건물에서 생산)하면 UI 변경 최소화 가능.

---

### 1-8. BuildingPlacementUI 구조

**파일**: `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

건물 배치 UI도 확인 필요. 신규 건물이 배치 메뉴에 표시되려면 새 BuildingType이 배치 버튼에 연결되어야 함.

---

## 2. 중요 설계 결정 사항 (구현 전 확인 필요)

### 결정 1 — 종족당 배럭 생산 유닛 수

현재 배럭 생산 UI는 3개 유닛 고정.
신규 유닛(Tank, CannonCart, DustSpirit 등)을 어떤 방식으로 제공할지:

**선택지 A**: 각 종족 배럭에서 기존 3종 유지, 신규 유닛은 특수 건물(TechnicalLaboratory, MistShrine 등)에서 생산
- 장점: ProductionPanelUI 코드 변경 없음
- 단점: 신규 건물별로 생산 UI를 별도 구현해야 함

**선택지 B**: 배럭 생산 유닛을 4개 이상으로 확장 (스크롤 또는 탭 추가)
- 장점: 기존 배럭 패턴 유지
- 단점: ProductionPanelUI 구조적 수정 필요

### 결정 2 — 신규 건물 기능 구현 범위

현재 건물은 Castle(본기지), Barracks(유닛 생산), MiningPost(채굴)만 기능이 구현되어 있음.
신규 건물(AutoTower=자동 포탑, FlightFacility=지원, HealShrine=힐 등)은 각각 새로운 게임 로직을 필요로 함.

이번 통합 작업에서 어느 범위까지 구현할지 결정 필요:
- **최소 범위**: 배치(시각적 표시)만 작동, 기능(포탑 공격, 힐 등)은 추후
- **전체 범위**: 각 건물 기능까지 구현

---

## 3. 파일별 변경 요약

| 파일 | 변경 유형 | 난이도 |
|------|----------|--------|
| `Domain/Unit/UnitType.cs` | enum 값 9개 추가 | 낮음 |
| `Domain/Building/BuildingType.cs` | enum 값 추가 | 낮음 |
| `Resources/Config/UnitStatsConfig.asset` | Inspector에서 항목 추가 | 낮음 (스탯 입력 필요) |
| `Resources/Config/BuildingStatsConfig.asset` | Inspector에서 항목 추가 | 낮음 (스탯 입력 필요) |
| `Editor/SetupUnitFactoryPrefabs.cs` | `_raceMappings` 테이블에 신규 유닛 추가 | 낮음 |
| `Infrastructure/Factories/BuildingFactory.cs` | struct 필드 추가 + switch 분기 추가 | 중간 |
| `Presentation/UI/ProductionPanelUI.cs` | 종족당 유닛 수 결정에 따라 변경 범위 결정 | 중간~높음 |
| UI Portrait 스프라이트 연결 (Inspector) | 신규 유닛 초상화 이미지 → Inspector 연결 | 낮음 (이미지 준비 필요) |

---

## 4. 신규 유닛 초상화(Portrait) 이미지 요구사항

`ProductionPanelUI`의 `UnitPortraitSet`은 각 유닛의 **2D 초상화 Sprite**를 사용.
현재 각 종족당 3개 초상화가 Inspector에 연결되어 있음.

신규 유닛들의 초상화 이미지가 준비되어야 Inspector 연결 가능:
- Human 신규: Tank, CannonCart 초상화
- Spirit 신규: DustSpirit, BoulderSpirit, QuakeSpirit 초상화
- Transcendence 신규: RhinoBreaker, EagleArcher, RabbitTrickster, MushroomBomber 초상화

이미지 사양: 1024×1024 PNG, 흰색/투명 배경 (CommonAssetGuide.md 참조).
