# Plan: 종족 선택 → 인게임 적용

**작업일**: 2026-04-07  
**작업명**: faction-ingame-apply  
**담당 에이전트**: game-programmer

---

## 구현 방향

`UnitFactory` / `BuildingFactory` 모두 스폰 시 팀의 종족(`GameRaceContext`)을 참조해 올바른 프리팹을 선택하도록 변경.  
`GameBootstrapper` 싱글플레이 경로에 `GameRaceContext.Set()` 1줄 추가.  
`UnitData`, `UnitSpawnUseCase`, 네트워크 코드는 변경 없음.

### 건물 종족 매핑 (사용자 확정 2026-04-07)

| BuildingType | Human | Spirit | Transcendence |
|-------------|-------|--------|---------------|
| Castle | Building_Castle | Building_SpiritNexus | Building_ElderTree |
| Barracks | Building_Barracks | Building_SummoningAltar | Building_HunterPlant |
| MiningPost | Building_MiningPost | Building_ManaRift | Building_FungalNode |

- MiningPost는 팀 구분 없음 (Spirit/Transcendence도 팀별 Blue/Red 프리팹 존재하나 팀 무관 1개 사용)

---

## 수정 파일 목록

### 파일 1: `UnitFactory.cs`
**경로**: `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`

#### 변경 1-A: `UnitTeamPrefabSet` 구조체 — 변경 없음
기존 구조체(pistoleer/assault/sniper)를 그대로 유지.

#### 변경 1-B: Inspector 필드 교체
```
[기존]
[SerializeField] private UnitTeamPrefabSet _bluePrefabs;
[SerializeField] private UnitTeamPrefabSet _redPrefabs;

[변경 후]
// Human
[SerializeField] private UnitTeamPrefabSet _humanBluePrefabs;
[SerializeField] private UnitTeamPrefabSet _humanRedPrefabs;
// Spirit
[SerializeField] private UnitTeamPrefabSet _spiritBluePrefabs;
[SerializeField] private UnitTeamPrefabSet _spiritRedPrefabs;
// Transcendence
[SerializeField] private UnitTeamPrefabSet _transcendenceBluePrefabs;
[SerializeField] private UnitTeamPrefabSet _transcendenceRedPrefabs;
```
- 기존 `_bluePrefabs`, `_redPrefabs` 필드 제거
- 필드명 변경으로 Inspector 연결 초기화됨 → [5-2] 에디터 스크립트로 재연결

#### 변경 1-C: `CreateUnitObject()` — 프리팹 선택 로직 교체
```csharp
// [기존] 팀만 구분
var set = unitData.Team == TeamId.Blue ? _bluePrefabs : _redPrefabs;

// [변경 후] 팀 + 종족(GameRaceContext) 구분
RaceId race = unitData.Team == TeamId.Blue
    ? GameRaceContext.BlueRace
    : GameRaceContext.RedRace;

UnitTeamPrefabSet set = (race, unitData.Team) switch
{
    (RaceId.Human,         TeamId.Blue) => _humanBluePrefabs,
    (RaceId.Human,         TeamId.Red)  => _humanRedPrefabs,
    (RaceId.Spirit,        TeamId.Blue) => _spiritBluePrefabs,
    (RaceId.Spirit,        TeamId.Red)  => _spiritRedPrefabs,
    (RaceId.Transcendence, TeamId.Blue) => _transcendenceBluePrefabs,
    (RaceId.Transcendence, TeamId.Red)  => _transcendenceRedPrefabs,
    _ => _humanBluePrefabs  // 안전망: 기본값 Human Blue
};
```

#### 변경 1-D: `InitializeUnitView()` — 프리팹 참조 없음, 변경 불필요
`InitializeUnitView()`는 이미 생성된 GameObject를 초기화만 하므로 변경 없음.

---

### 파일 2: `GameBootstrapper.cs`
**경로**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

#### 변경 위치: `Start()` 싱글플레이 분기 (L261~264)

```csharp
// [기존]
else
{
    // 싱글플레이 모드: 기존 로직 그대로 실행
    LoadMap(HexOrientation.FlatTop);
}

// [변경 후]
else
{
    // 싱글플레이: 로비에서 선택한 종족을 Blue에 적용.
    // Red는 AI/기본값이므로 Human으로 설정.
    GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human);

    // 싱글플레이 모드: 기존 로직 그대로 실행
    LoadMap(HexOrientation.FlatTop);
}
```

---

---

### 파일 3: `BuildingFactory.cs`
**경로**: `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`

#### 변경 3-A: `BuildingTeamPrefabSet` 구조체 — 변경 없음
기존 구조체(castle/barracks)를 그대로 유지.

#### 변경 3-B: Inspector 필드 교체
```
[기존]
[SerializeField] private BuildingTeamPrefabSet _bluePrefabs;
[SerializeField] private BuildingTeamPrefabSet _redPrefabs;

[변경 후]
// Human
[SerializeField] private BuildingTeamPrefabSet _humanBluePrefabs;
[SerializeField] private BuildingTeamPrefabSet _humanRedPrefabs;
// Spirit
[SerializeField] private BuildingTeamPrefabSet _spiritBluePrefabs;
[SerializeField] private BuildingTeamPrefabSet _spiritRedPrefabs;
// Transcendence
[SerializeField] private BuildingTeamPrefabSet _transcendenceBluePrefabs;
[SerializeField] private BuildingTeamPrefabSet _transcendenceRedPrefabs;
```
- `_miningPostPrefab`은 종족 무관이므로 그대로 유지

#### 변경 3-C: `CreateBuildingObject()` — 프리팹 선택 로직 교체
```csharp
// [기존] 팀만 구분
var set = data.Team == TeamId.Blue ? _bluePrefabs : _redPrefabs;

// [변경 후] 팀 + 종족(GameRaceContext) 구분
RaceId race = data.Team == TeamId.Blue
    ? GameRaceContext.BlueRace
    : GameRaceContext.RedRace;

BuildingTeamPrefabSet set = (race, data.Team) switch
{
    (RaceId.Human,         TeamId.Blue) => _humanBluePrefabs,
    (RaceId.Human,         TeamId.Red)  => _humanRedPrefabs,
    (RaceId.Spirit,        TeamId.Blue) => _spiritBluePrefabs,
    (RaceId.Spirit,        TeamId.Red)  => _spiritRedPrefabs,
    (RaceId.Transcendence, TeamId.Blue) => _transcendenceBluePrefabs,
    (RaceId.Transcendence, TeamId.Red)  => _transcendenceRedPrefabs,
    _                                   => _humanBluePrefabs  // 안전망: 기본값 Human Blue
};
```
- MiningPost는 기존과 동일하게 `_miningPostPrefab` 사용

---

## Inspector 재연결 계획 [5-2]

필드명 변경으로 기존 프리팹 연결이 초기화된다. 에디터 1회성 스크립트로 자동 연결:

**스크립트명**: `Editor/SetupUnitFactoryPrefabs.cs` (기존 파일에 BuildingFactory 섹션 추가)  
**메뉴 경로 추가**: `Hexiege/Setup/BuildingFactory 프리팹 연결`

자동 연결 대상 프리팹 경로 (유닛 18개 + 건물 12개):

| 필드 | 프리팹 경로 |
|------|------------|
| `_humanBluePrefabs.pistoleer` | `Units/Human/Unit_Pistoleer_Blue.prefab` |
| `_humanBluePrefabs.assault` | `Units/Human/Unit_Assault_Blue.prefab` |
| `_humanBluePrefabs.sniper` | `Units/Human/Unit_Sniper_Blue.prefab` |
| `_humanRedPrefabs.pistoleer` | `Units/Human/Unit_Pistoleer_Red.prefab` |
| `_humanRedPrefabs.assault` | `Units/Human/Unit_Assault_Red.prefab` |
| `_humanRedPrefabs.sniper` | `Units/Human/Unit_Sniper_Red.prefab` |
| `_spiritBluePrefabs.pistoleer` | `Units/Spirit/Unit_EmberSpirit_Blue.prefab` |
| `_spiritBluePrefabs.assault` | `Units/Spirit/Unit_FlameSpirit_Blue.prefab` |
| `_spiritBluePrefabs.sniper` | `Units/Spirit/Unit_InfernoSpirit_Blue.prefab` |
| `_spiritRedPrefabs.pistoleer` | `Units/Spirit/Unit_EmberSpirit_Red.prefab` |
| `_spiritRedPrefabs.assault` | `Units/Spirit/Unit_FlameSpirit_Red.prefab` |
| `_spiritRedPrefabs.sniper` | `Units/Spirit/Unit_InfernoSpirit_Red.prefab` |
| `_transcendenceBluePrefabs.pistoleer` | `Units/Transcendence/Unit_FoxMagician_Blue.prefab` |
| `_transcendenceBluePrefabs.assault` | `Units/Transcendence/Unit_BearGuard_Blue.prefab` |
| `_transcendenceBluePrefabs.sniper` | `Units/Transcendence/Unit_LionKnight_Blue.prefab` |
| `_transcendenceRedPrefabs.pistoleer` | `Units/Transcendence/Unit_FoxMagician_Red.prefab` |
| `_transcendenceRedPrefabs.assault` | `Units/Transcendence/Unit_BearGuard_Red.prefab` |
| `_transcendenceRedPrefabs.sniper` | `Units/Transcendence/Unit_LionKnight_Red.prefab` |

**건물 프리팹 연결 (12개):**

| 필드 | 프리팹 경로 |
|------|------------|
| `_humanBluePrefabs.castle` | `Buildings/Human/Building_Castle_Blue.prefab` |
| `_humanBluePrefabs.barracks` | `Buildings/Human/Building_Barracks_Blue.prefab` |
| `_humanRedPrefabs.castle` | `Buildings/Human/Building_Castle_Red.prefab` |
| `_humanRedPrefabs.barracks` | `Buildings/Human/Building_Barracks_Red.prefab` |
| `_spiritBluePrefabs.castle` | `Buildings/Spirit/Building_SpiritNexus_Blue.prefab` |
| `_spiritBluePrefabs.barracks` | `Buildings/Spirit/Building_SummoningAltar_Blue.prefab` |
| `_spiritRedPrefabs.castle` | `Buildings/Spirit/Building_SpiritNexus_Red.prefab` |
| `_spiritRedPrefabs.barracks` | `Buildings/Spirit/Building_SummoningAltar_Red.prefab` |
| `_transcendenceBluePrefabs.castle` | `Buildings/Transcendence/Building_ElderTree_Blue.prefab` |
| `_transcendenceBluePrefabs.barracks` | `Buildings/Transcendence/Building_HunterPlant_Blue.prefab` |
| `_transcendenceRedPrefabs.castle` | `Buildings/Transcendence/Building_ElderTree_Red.prefab` |
| `_transcendenceRedPrefabs.barracks` | `Buildings/Transcendence/Building_HunterPlant_Red.prefab` |

- MiningPost(`_miningPostPrefab`)는 종족 무관, 기존 1개 그대로 유지

---

## 구현 순서

1. `BuildingFactory.cs` — 필드 교체 + 선택 로직 변경
2. `SetupUnitFactoryPrefabs.cs` — BuildingFactory 프리팹 연결 메서드 추가
3. 사용자가 `Hexiege/Setup/BuildingFactory 프리팹 연결` 실행
4. [5-3] QA 테스트

---

## 아키텍처 제약 확인

- `UnitFactory`는 Infrastructure 레이어 → `GameRaceContext`(Infrastructure)를 참조하므로 레이어 규칙 위반 없음
- `GameBootstrapper`는 Bootstrap 레이어 → 모든 레이어 참조 가능
- `GameRaceContext`는 Infrastructure의 정적 홀더이므로 `using Hexiege.Infrastructure` 추가 필요 (UnitFactory는 이미 import 중)

---

## 위험 요소 및 대응

| 위험 | 대응 |
|------|------|
| `_bluePrefabs` 필드 제거 시 직렬화 데이터 손실 | 에디터 스크립트로 재연결하여 복구 |
| NetworkProductionController 등에서 UnitFactory 프리팹 세트에 직접 접근하는 코드 없음 | 별도 변경 불필요 (확인 완료) |
| Spirit/Transcendence 프리팹에 NetworkObject, NetworkUnit 컴포넌트 미부착 가능성 | 멀티플레이 테스트 시 별도 확인 필요 |
