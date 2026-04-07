# Research: 종족 선택 → 인게임 적용

**작업일**: 2026-04-07  
**작업명**: faction-ingame-apply  
**목표**: 로비에서 선택한 종족이 실제 게임에서 해당 종족의 유닛 프리팹으로 스폰되도록 연결

---

## 1. 현재 상태 분석

### 1.1 이미 구현된 부분

| 항목 | 파일 | 내용 |
|------|------|------|
| 종족 enum 정의 | `Domain/Common/RaceId.cs` | Human(0), Spirit(1), Transcendence(2) |
| 로비 선택값 저장 | `Infrastructure/LocalPlayerRace.cs` | 로비에서 선택한 종족 로컬 저장 |
| 게임 실행 중 종족 저장 | `Infrastructure/GameRaceContext.cs` | BlueRace / RedRace 정적 홀더 |
| 멀티플레이 동기화 | `Infrastructure/Network/NetworkGameFlow.cs` | RequestReadyServerRpc → StartGameClientRpc에서 양 팀 종족 교환 후 GameRaceContext.Set() 호출 |
| 종족별 유닛 프리팹 폴더 | `Assets/_Project/Prefabs/Units/` | Human/Spirit/Transcendence 각 팀별 6개씩 완비 |

### 1.2 미구현 — 게임 적용 누락 지점

#### 문제 1: UnitFactory — 종족 무시, 항상 Human 프리팹 사용 (핵심)
**파일**: `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` (L82~91, L161~168)

현재 Inspector 구조:
```
[SerializeField] private UnitTeamPrefabSet _bluePrefabs;  ← Human Blue 프리팹만 연결됨
[SerializeField] private UnitTeamPrefabSet _redPrefabs;   ← Human Red 프리팹만 연결됨
```

현재 프리팹 선택 로직 (`CreateUnitObject`, L161~168):
```csharp
var set = unitData.Team == TeamId.Blue ? _bluePrefabs : _redPrefabs;
// ↑ 팀만 구분, 종족(Race) 정보 전혀 사용 안 함
```

`GameRaceContext`는 이미 양 팀의 종족 정보를 갖고 있지만, `UnitFactory`가 이를 참조하지 않음.

#### 문제 2: GameBootstrapper 싱글플레이 — GameRaceContext 미초기화
**파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` (L261~265)

```csharp
// 싱글플레이 시
LoadMap(HexOrientation.FlatTop);
// ↑ GameRaceContext.Set()이 호출되지 않아
//   GameRaceContext.BlueRace, RedRace 기본값(Human)이 유지됨
//   → 로비에서 Spirit 선택해도 Human 프리팹 스폰
```

멀티플레이는 `NetworkGameFlow.StartGameClientRpc()`에서 이미 `GameRaceContext.Set()`을 호출하고 있어 문제 없음.

---

## 2. 종족별 프리팹 구조

```
Assets/_Project/Prefabs/Units/
├── Human/
│   ├── Unit_Pistoleer_Blue.prefab   ← UnitType.Pistoleer, TeamId.Blue
│   ├── Unit_Pistoleer_Red.prefab
│   ├── Unit_Assault_Blue.prefab     ← UnitType.Assault, TeamId.Blue
│   ├── Unit_Assault_Red.prefab
│   ├── Unit_Sniper_Blue.prefab      ← UnitType.Sniper, TeamId.Blue
│   └── Unit_Sniper_Red.prefab
├── Spirit/
│   ├── Unit_EmberSpirit_Blue.prefab   ← UnitType.Pistoleer 역할 (경량 원거리)
│   ├── Unit_EmberSpirit_Red.prefab
│   ├── Unit_FlameSpirit_Blue.prefab   ← UnitType.Assault 역할 (근접 탱커)
│   ├── Unit_FlameSpirit_Red.prefab
│   ├── Unit_InfernoSpirit_Blue.prefab ← UnitType.Sniper 역할 (장거리 딜러)
│   └── Unit_InfernoSpirit_Red.prefab
└── Transcendence/
    ├── Unit_FoxMagician_Blue.prefab   ← UnitType.Pistoleer 역할
    ├── Unit_FoxMagician_Red.prefab
    ├── Unit_BearGuard_Blue.prefab     ← UnitType.Assault 역할
    ├── Unit_BearGuard_Red.prefab
    ├── Unit_LionKnight_Blue.prefab    ← UnitType.Sniper 역할
    └── Unit_LionKnight_Red.prefab
    (+ 장비 프리팹 6개: BearShield, FoxStaff, LionSword × 2팀)
```

**UnitType(역할) ↔ 종족 유닛 매핑:**

| UnitType | Human | Spirit | Transcendence |
|----------|-------|--------|---------------|
| Pistoleer | Unit_Pistoleer | Unit_EmberSpirit | Unit_FoxMagician |
| Assault | Unit_Assault | Unit_FlameSpirit | Unit_BearGuard |
| Sniper | Unit_Sniper | Unit_InfernoSpirit | Unit_LionKnight |

---

## 3. 데이터 흐름 (현재 vs 수정 후)

### 현재 (깨진 흐름)
```
로비: LocalPlayerRace.Set(Spirit)
  ↓ 게임 시작
[싱글] GameBootstrapper.LoadMap()
  → GameRaceContext는 기본값(Human, Human) 그대로
  ↓
UnitFactory.CreateUnitObject(unitData)
  → _bluePrefabs.pistoleer (Human Pistoleer) 선택
  → Spirit를 선택했는데 Human 유닛 스폰 ❌
```

### 수정 후 (정상 흐름)
```
로비: LocalPlayerRace.Set(Spirit)
  ↓ 게임 시작
[싱글] GameBootstrapper.LoadMap()
  → GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human) ← 추가
  ↓
UnitFactory.CreateUnitObject(unitData)
  → GameRaceContext에서 팀별 종족 조회 (Blue→Spirit, Red→Human)
  → _spiritBluePrefabs.pistoleer (EmberSpirit Blue) 선택 ✅
```

---

## 4. 영향 범위 분석

### 수정 파일
| 파일 | 수정 내용 | 범위 |
|------|---------|------|
| `Infrastructure/Factories/UnitFactory.cs` | 종족별 프리팹 세트 추가 + 선택 로직 변경 | 핵심 |
| `Bootstrap/GameBootstrapper.cs` | LoadMap() 싱글플레이 경로에 GameRaceContext.Set() 추가 | 1줄 추가 |

### 수정 불필요 파일
| 파일 | 이유 |
|------|------|
| `Domain/Unit/UnitData.cs` | UnitFactory가 GameRaceContext에서 직접 종족 조회 → UnitData에 Race 필드 불필요 |
| `Application/UseCases/UnitSpawnUseCase.cs` | SpawnUnit() 시그니처 변경 없음 |
| `Domain/Unit/UnitStats.cs` | 스탯은 UnitType 기준 유지 (종족별 스탯 차이는 UI 완성 후 별도 작업) |
| `Infrastructure/Network/NetworkGameFlow.cs` | 이미 GameRaceContext.Set() 호출 중 |
| 기타 네트워크 컨트롤러 | 변경 없음 |

### Inspector 재연결 필요
`UnitFactory` Inspector에 종족별 프리팹 6세트(3종족 × 2팀 = 6개 UnitTeamPrefabSet)를 새로 연결해야 함.
기존 `_bluePrefabs`, `_redPrefabs` 필드명이 변경되므로 에디터 스크립트로 자동화 예정.

---

## 5. 스코프 제한 (UI 미제작 고려)

- **이번 작업 범위**: 유닛 스폰 시 올바른 종족 프리팹 선택
- **미포함**: 종족별 유닛 생산 UI (생산 패널에서 종족 아이콘/이름 표시) — UI 제작 후 별도 작업
- **미포함**: 종족별 스탯 차이 — 현재 모든 종족이 동일 스탯(UnitStats) 사용, 추후 별도 작업
- **미포함**: 종족별 생산 비용/시간 차이 — 추후 별도 작업

---

## 6. 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| Inspector 직렬화 깨짐 | 기존 `_bluePrefabs` 필드명 변경 시 Inspector 연결 초기화 | 에디터 1회성 스크립트로 재연결 |
| 멀티플레이 Red 기본값 | 싱글에서 Red(AI) 기본값을 Human으로 설정 — 현재 AI 없으므로 문제 없음 | 추후 AI 구현 시 별도 처리 |
| 프리팹 누락 | 종족별 프리팹이 Inspector에 연결 안 된 경우 null 오류 | 기존 LogError 처리 그대로 유지 |
