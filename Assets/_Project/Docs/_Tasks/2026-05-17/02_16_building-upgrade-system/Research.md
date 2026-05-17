# Research — 건물 업그레이드 시스템 및 단계별 생산건물 에셋 적용

## 이 작업이 무엇인지

새로 제작된 생산 건물 3D 에셋들을 인게임에 적용하는 작업이다.
핵심은 단순 에셋 등록이 아니라, 현재 "Barracks"라는 하나의 카테고리로 뭉쳐있던 생산건물 시스템을
**종족별 생산 라인 × 단계(1/2/3)**로 구분하도록 확장하는 것이다.

예시:
- 총기류 라인: Gunsmith(1단계) → Armory(2단계) → WeaponForge(3단계)
- 근접류 라인A: TrainingCamp(1단계) → WarAcademy(2단계) → Barracks(3단계)
- 정령 불 라인: FireSpire(1단계) → BlazeConduit(2단계) → InfernoCore(3단계)

이 구조가 동작하려면:
- 건물 배치 패널 → 각 라인의 1단계만 표시
- 유닛 생산 패널 → 현재 건물의 단계에 맞는 유닛만 표시 + 업그레이드 버튼
- 건물 팩토리 → 어떤 특정 건물인지 알고 해당 3D 프리팹을 불러올 수 있어야 함

---

## 현재 시스템 분석

### 1. BuildingType 열거형 (`Assets/_Project/Scripts/Domain/Building/BuildingType.cs`)

```
Castle, Barracks, MiningPost, AutoTower, FlightFacility, Research, MagicBuilding, AncientGrove, HealShrine
```

**핵심 문제**: 모든 생산건물(TrainingCamp, WarAcademy, Gunsmith, Armory 등)이 단 하나의 값 `Barracks`로 처리된다.
어떤 특정 건물인지(라인/단계) 구분할 방법이 없다.

---

### 2. BuildingFactory (`Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`)

Inspector에서 종족(Human/Spirit/Transcendence)별 리스트를 관리하며, 각 항목은 `BuildingType + Blue 프리팹 + Red 프리팹` 구조다.

현재 구조:
```
_humanPrefabs:  [ (Barracks, BlueBarracks, RedBarracks), (Castle, ...), ... ]
_spiritPrefabs: [ (Barracks, BlueSpirit, RedSpirit), ... ]
```

모든 Barracks 타입 건물이 하나의 프리팹으로만 표현된다.
TrainingCamp와 WarAcademy를 다른 프리팹으로 표시할 수단이 없다.

---

### 3. BuildingData (`Assets/_Project/Scripts/Domain/Building/BuildingData.cs`)

현재 필드: `Id`, `Type (BuildingType)`, `Team`, `Position`, `MaxHp`, `Hp`

"단계(Stage)" 또는 "어떤 라인인지" 를 저장하는 필드가 없다.
코드 주석에 "향후 확장: Level, 업그레이드 상태"라고 명시되어 있어 확장을 예상하고 있었음.

---

### 4. BuildingStats (`Assets/_Project/Scripts/Domain/Building/BuildingStats.cs`)

`(BuildingType, RaceId)` 조합으로 HP/골드비용/공격력을 반환한다.
BuildingType이 Barracks 하나이므로 모든 생산건물이 동일한 스탯을 공유한다.
단계별로 건설 비용/HP를 다르게 설정하려면 키 구조 변경이 필요하다.

---

### 5. BuildingStatsConfig (`Assets/_Project/Scripts/Infrastructure/Config/BuildingStatsConfig.cs`)

ScriptableObject. `List<BuildingTypeEntry>` 형태로 BuildingType별 종족별 스탯을 정의한다.
각 항목 = BuildingType + Human/Spirit/Transcendence별 MaxHp, GoldCost, AttackPower.

---

### 6. BuildingPlacementUI (`Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`)

Inspector에서 팀 × 종족별로 6개의 `List<BuildingPortraitEntry>` 리스트를 가진다.
각 항목: `BuildingType + Sprite(아이콘)`.

현재 구조:
```
_blueHumanBuildings: [ (Barracks, icon), (MiningPost, icon) ]
```

생산 라인이 여러 개가 되면, 현재 방식으로는 "1단계만 표시"를 구현할 수 없다.
모든 라인의 1단계(TrainingCamp, Gunsmith, Garage 등)를 각 리스트에 명시해야 하므로
리스트 내용을 재구성해야 한다.

---

### 7. ProductionPanelUI (`Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`)

배럭 클릭 시 팀 × 종족 조합으로 고정된 유닛 목록을 표시한다.
현재 구조:
```
_blueHumanUnits: [ (LittleKnight, portrait), (Pistoleer, portrait), ... ]
```
6개 고정 리스트로 모든 배럭이 동일한 유닛을 보여준다.

문제:
- TrainingCamp를 클릭해도, Gunsmith를 클릭해도 같은 유닛 목록이 나온다.
- 건물 단계에 따라 유닛 잠금 처리하는 로직 자체가 없다.
- `_currentBarracks`는 `BuildingData`를 보관하지만, 현재는 `Id`만 사용한다.
  `Type`을 통해 어떤 건물인지 확인하려 해도 모든 생산건물이 `Barracks`이므로 구분 불가.

업그레이드 버튼도 없다.

**확정된 표시 방식**: 라인의 전체 유닛을 모두 표시하되, 현재 건물 단계보다 높은 단계에서 해금되는 유닛은 잠금 상태로 표시한다. 잠금 유닛을 탭하면 생산 대신 토스트 메시지로 안내한다.

---

### 8. NetworkBuildingController (`Assets/_Project/Scripts/Infrastructure/Network/NetworkBuildingController.cs`)

`BuildingType`을 int로 직렬화해서 ServerRpc/ClientRpc를 통해 전송한다.
`BuildingType` 열거형 값이 변경되면 네트워크 통신도 영향을 받는다.

---

## 핵심 문제 요약

| 문제 | 위치 |
|------|------|
| 모든 생산건물이 `Barracks` 하나로 뭉쳐있어 구분 불가 | `BuildingType` 열거형 |
| 특정 건물 프리팹을 선택할 수단 없음 | `BuildingFactory` |
| 건물의 단계/라인 정보 없음 | `BuildingData` |
| 단계별 스탯 구분 불가 | `BuildingStats`, `BuildingStatsConfig` |
| 생산 라인 1단계만 배치 패널에 표시하는 로직 없음 | `BuildingPlacementUI` |
| 건물 종류별 유닛 필터링 없음, 업그레이드 버튼 없음 | `ProductionPanelUI` |

---

## 영향 범위

변경이 필요한 파일:

| 파일 | 변경 이유 |
|------|-----------|
| `BuildingType.cs` | 특정 건물 이름을 열거형 값으로 추가 |
| `BuildingData.cs` | Stage 정보 추가 |
| `BuildingStats.cs` | 새 BuildingType 키에 대한 폴백 스탯 추가 |
| `BuildingStatsConfig.cs` | 단계별 스탯 항목 추가 가능하도록 |
| `BuildingFactory.cs` | 특정 건물 타입별 프리팹 Inspector 리스트 재구성 |
| `BuildingPlacementUI.cs` | 배치 패널에 생산 라인 1단계만 노출하도록 리스트 재구성 |
| `ProductionPanelUI.cs` | 건물 타입별 유닛 목록 + 업그레이드 버튼 추가 |
| `BuildingPlacementUseCase.cs` | Barracks 카테고리 판별 로직 업데이트 |
| `NetworkBuildingController.cs` | BuildingType int 변환 — 열거형 순서 주의 |

BuildingStatsConfig ScriptableObject 에셋 (Inspector 재설정 필요):
- `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`

BuildingFactory, BuildingPlacementUI Inspector 재설정 필요:
- 씬 내 컴포넌트 직접 재연결

---

## 참고: AssetList.md 기준 생산건물 라인 구성

### Human
| 라인 | 1단계 | 2단계 | 3단계 |
|------|-------|-------|-------|
| 근거리A | TrainingCamp | WarAcademy | HumanBarracks |
| 총기류 | Gunsmith | Armory | WeaponForge |
| 탈것류 | Garage | VehicleBay | — |

### Spirit
| 라인 | 1단계 | 2단계 | 3단계 |
|------|-------|-------|-------|
| 불 속성 | FireSpire | BlazeConduit | InfernoCore |
| 물 속성 | AquaSpring | TidalNexus | OceanicHeart |
| 땅 속성 | StoneMound | TerraForge | GaeaSanctum |

### Transcendence
| 라인 | 1단계 | 2단계 | 3단계 |
|------|-------|-------|-------|
| 동물A | PrimalAltar | PrimalDen | PrimalSanctuary(제작예정) |
| 동물B | FeralAltar | FeralDen | FeralSanctuary |
| 식물 | SporePatch | FloralNursery | — |

---

## 미결 항목 (Plan 작성 전 사용자 확인 필요)

1. **BuildingType 확장 방식**: 기존 `Barracks`를 대체하는 구체적인 타입 이름을 추가할 것인가?
   아니면 별도 `BuildingLineId` 열거형을 추가하는 이중 구조를 사용할 것인가?
2. **단계별 유닛 매핑**: 어떤 건물이 어떤 유닛을 생산하는지 기획이 확정되어야 Plan에 반영 가능.
   (예: TrainingCamp → LittleKnight만? TrainingCamp → LittleKnight + SpearMan?)
3. **업그레이드 조건**: 업그레이드에 골드 비용이 드는가? 업그레이드 시 기존 건물을 파괴하고 교체하는가?
