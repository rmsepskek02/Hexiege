# Research — 코드 구조 개선 Phase 2

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

Phase 1에서 코드 클린업(불필요한 코드 정리)을 끝냈고, 이번 Phase 2에서는 "구조"를 손봅니다.
즉, 동작은 그대로 두면서 나중에 건물이나 종족을 추가할 때 실수하기 쉬운 부분, 같은 일을 두 번 하는 부분을 정리해 유지보수가 쉬워지도록 만드는 작업입니다.

이번에 다루는 문제는 두 가지입니다.

1. **건물 정보가 세 군데에 흩어져 있는 문제 (BuildingTypeHelper)**
   현재 "이 건물이 유닛을 생산하는 건물인가?", "이 건물은 몇 단계인가?", "이 건물의 다음 단계는 무엇인가?"라는
   세 가지 질문에 답하는 코드가 각각 따로 거대한 분기문(switch)으로 작성되어 있습니다.
   문제는 건물을 하나 새로 추가하면 이 세 곳을 빠짐없이 모두 고쳐야 한다는 점입니다.
   한 곳이라도 빠뜨리면 "생산 건물로는 인식되는데 단계는 0으로 나오는" 식의 모순이 생깁니다.
   이를 "건물 하나당 정보 한 줄"로 모아 둔 표(테이블) 형태로 바꿔서, 새 건물 추가 시 한 곳만 고치면 되도록 만듭니다.

2. **헥스 타일 설정을 두 번 적용하는 문제 (HexMetrics 중복 setup)**
   멀티플레이로 게임을 시작할 때, 헥스 타일의 방향/크기 같은 설정값을 한 번 직접 지정하고,
   그 직후 맵을 불러오는 과정에서 또 같은 설정을 적용합니다. 즉 같은 일을 두 번 합니다.
   동작 자체는 멀쩡하지만, 같은 설정 코드가 두 군데에 존재하기 때문에 한쪽만 수정하면 두 값이 어긋날 위험이 있습니다.
   이 중복을 정리합니다.

> 이 문서는 "현재 상태가 어떤가"만 기록합니다. 실제로 어떻게 바꿀지(구현 방안)는 Plan.md에 적습니다.

---

## 1. BuildingTypeHelper — switch 중복 현황

### 대상 파일
`Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs`
(Domain 레이어 — 순수 C#, Unity 의존 없음)

### 현재 구조
정적 헬퍼 클래스. 아래 메서드가 각각 독립된 switch 문으로 동일한 건물 목록을 반복 나열한다.

| 메서드 | 역할 | 분기 방식 |
|--------|------|-----------|
| `IsProductionBuilding(BuildingType)` | 생산 건물 여부 | switch — 생산 건물 24종을 case로 나열, default는 false |
| `GetStage(BuildingType)` | 단계(1/2/3, 비생산=0) | switch — 1/2/3단계별로 건물을 case로 나열, default는 0 |
| `GetNextStage(BuildingType)` | 다음 단계 타입(없으면 null) | switch — 라인별 1→2, 2→3 매핑, default는 null |
| `CanUpgrade(BuildingType)` | 업그레이드 가능 여부 | `GetNextStage().HasValue` 재사용 (switch 아님) |
| `CanShowActionPanel(BuildingType)` | 비생산 건물 액션 패널 표시 여부 | `IsProductionBuilding()` + Castle 예외 (switch 아님) |

### 핵심 문제 (한 건물 = 세 곳 수정)
같은 건물 정보가 세 switch에 분산되어 있어, 건물 1종 추가/변경 시 세 곳을 모두 손대야 한다.
예: `BuildingType.SporePatch`(식물 1단계)는 다음 3곳에 동시에 존재해야 한다.
- `IsProductionBuilding`의 case 목록 (BuildingTypeHelper.cs:62)
- `GetStage`의 1단계 case 목록 (BuildingTypeHelper.cs:95)
- `GetNextStage`의 `SporePatch → FloralNursery` 매핑 (BuildingTypeHelper.cs:164)

한 곳만 누락되면 "생산 건물로 인식되지만 단계가 0", "단계는 1인데 업그레이드 경로가 없음" 같은 데이터 불일치가 발생한다.

### 데이터의 실제 모양 (생산 건물 25종)
현재 switch에 담긴 정보를 정리하면 "라인별 단계 체인" 구조다.

| 종족 | 라인 | 1단계 | 2단계 | 3단계 |
|------|------|-------|-------|-------|
| Human | 근거리A | TrainingCamp | WarAcademy | HumanBarracks |
| Human | 총기류 | Gunsmith | Armory | WeaponForge |
| Human | 탈것류 | Garage | VehicleBay | (없음) |
| Spirit | 불 | FireSpire | BlazeConduit | InfernoCore |
| Spirit | 물 | AquaSpring | TidalNexus | OceanicHeart |
| Spirit | 땅 | StoneMound | TerraForge | GaeaSanctum |
| Transcendence | 동물A | PrimalAltar | PrimalDen | PrimalSanctuary |
| Transcendence | 동물B | FeralAltar | FeralDen | FeralSanctuary |
| Transcendence | 식물 | SporePatch | FloralNursery | (없음) |

> 주의: `IsProductionBuilding`의 case 목록에는 `PrimalSanctuary`가 **포함되지 않은 것으로 보인다**
> (BuildingTypeHelper.cs:56~63 확인 결과 PrimalDen 다음이 FeralAltar로 이어짐).
> 반면 `GetStage`의 3단계 case(:111~118)와 `GetNextStage`의 `PrimalDen → PrimalSanctuary`(:159)에는 존재한다.
> BuildingType.cs:68에는 `PrimalSanctuary = 26, // 동물A 3단계 (제작 예정)` 로 정의되어 있다.
> 이 불일치가 의도된 것인지(제작 예정이라 생산 목록 제외) 누락 버그인지 **Plan 작성 시 사용자 확인 필요**.
> 바로 이 불일치야말로 "세 곳 분산 관리"의 위험을 보여주는 실제 사례다.

### 비생산 건물 (단계 개념 없음, 7종)
Castle(0), MiningPost(1), AutoTower(2), FlightFacility(3), Research(4), MagicBuilding(5), HealShrine(6).
이들은 세 메서드 모두 default 분기로 처리된다 (생산 여부 false, 단계 0, 다음 단계 null).

### 영향 범위 (이 헬퍼를 호출하는 곳)
구조 변경 시 메서드 **시그니처와 반환값이 동일하게 유지**되면 호출부는 영향받지 않는다.
실제 호출 확인 지점:
- `GameBootstrapper.Setup.cs` — `GetNextStage()`를 철거 환불 누적 비용 캐싱에 사용 (Setup.cs:201).
  또한 1단계 건물 목록(stage1Buildings)과 비생산 건물 목록(nonProductionBuildings)을 **별도 하드코딩 배열**로 보유 중(:176~187, :218~226).
  → 이 배열들도 새 lookup table에서 파생 가능하나, 이번 작업 범위에 포함할지는 Plan에서 판단.
- 그 외 `IsProductionBuilding` / `GetStage` / `CanShowActionPanel` 호출부는 UI/입력 계층에 존재 (시그니처 유지 시 무영향).

---

## 2. HexMetrics 중복 setup 현황

### 대상 파일
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Network.cs` (`StartNetworkGame`)
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` (`ApplyConfig`)
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs` (`LoadMap`, `ApplyConfig` 호출처)

### 중복 지점 정확한 위치

**(A) StartNetworkGame 내부 — 수동 설정 (Network.cs:64~67)**
```
HexMetrics.Orientation = HexOrientation.FlatTop;
HexOrientationContext.Current = HexOrientation.FlatTop;
HexMetrics.TileWidth = oc.TileWidth;
HexMetrics.TileHeight = oc.TileHeight;
```
바로 다음 `HexMetrics.GridCenter()`를 호출해 ViewConverter를 사전 설정하기 위해, FlatTop 기준 메트릭을 먼저 적용한다.

**(B) ApplyConfig — 정식 설정 (Setup.cs:247~255)**
```
HexMetrics.Orientation = orientation;
HexOrientationContext.Current = orientation;
HexMetrics.TileWidth = oc.TileWidth;
HexMetrics.TileHeight = oc.TileHeight;
HexMetrics.UnitYOffset = _config.UnitYOffset;   // ← (A)에는 없는 항목
```

### 호출 흐름
1. 멀티플레이: `StartNetworkGame(localTeam)` →
   (A) HexMetrics 수동 설정 4줄 → `GridCenter()` → `ViewConverter.Setup()` →
   `LoadMap(FlatTop)` → 내부에서 (B) `ApplyConfig()` 재실행 (Map.cs:77).
2. 싱글플레이: `LoadMap(FlatTop)`만 호출 → (B) `ApplyConfig()` 1회 → ViewConverter는 `isNetworkMode == false` 분기에서 설정 (Map.cs:82~87).

### 중복의 실체
멀티플레이 경로에서 `Orientation` / `TileWidth` / `TileHeight` 세 값이 (A)와 (B)에서 **연속 두 번** 설정된다.
- (A)에는 `HexMetrics.UnitYOffset` 설정이 빠져 있다 → (A)와 (B)는 완전히 동일하지 않다.
- (A)가 존재하는 유일한 이유: `LoadMap()`보다 먼저 `GridCenter()`를 호출해 ViewConverter를 사전 설정해야 하기 때문 (Network.cs:54~56 주석 명시).

### 위험
동작상 버그는 아니나, HexMetrics 설정 코드가 두 곳에 존재하여:
- 향후 메트릭 항목이 추가될 때(예: 새 오프셋) 한쪽만 갱신하면 멀티플레이에서 사전 계산값이 어긋날 수 있다.
- 실제로 이미 `UnitYOffset`이 (A)에 누락되어 있어 "부분 중복" 상태다.

### 영향 범위
- `ApplyConfig`는 `private`이며 `LoadMap` 내부에서만 호출된다 (Map.cs:77).
- `StartNetworkGame`은 `NetworkGameFlow.StartGameClientRpc()`가 호출하는 public 진입점이다 (Network.cs:38).
- 중복 제거 시 **"ViewConverter 사전 설정 시점에 HexMetrics가 FlatTop으로 준비되어 있어야 한다"는 제약**을 깨지 않아야 한다.

---

## 작업 시 확인이 필요한 사항 (Plan 단계 결정 대상)

1. `IsProductionBuilding`의 `PrimalSanctuary` 누락이 의도인지 버그인지 — 사용자 확인 필요.
   (lookup table로 통합하면 이 항목을 "포함/제외" 중 무엇으로 정의할지 명시해야 한다.)
2. `GameBootstrapper.Setup.cs`의 하드코딩 배열(stage1Buildings, nonProductionBuildings)을
   새 lookup table에서 파생시킬지, 이번 범위에서 제외할지.
3. HexMetrics 중복 제거 방식: (A)를 `ApplyConfig` 호출로 대체할지, ViewConverter 사전 설정 전용 경량 메서드로 분리할지.

> 위 사항들은 추정하지 않고 Plan 작성 전 사용자에게 확인한다 (CLAUDE.md 규칙 10, 12).
