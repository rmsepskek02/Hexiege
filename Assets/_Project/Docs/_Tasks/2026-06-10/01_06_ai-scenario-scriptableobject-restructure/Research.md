# Research — AI 시나리오 ScriptableObject 구조 개편

## 작업 목적

현재 AI 시나리오 에셋이 "종족 × 시나리오(A/B/C)" 형태로 9개 파일로 분리되어야 하는 구조인데,
Human용만 A/B/C 3개가 있고 Spirit/Transcendence는 없다.
이를 "종족당 1파일, 각 파일이 3개 시나리오를 포함"하는 구조로 바꾸고,
3종족 전체 에셋을 완성한다.

또한 기존 Human_B.asset, Human_C.asset이 Human_A.asset과 동일한 데이터를 담고 있어
Human 시나리오 B/C의 빌드오더도 이번에 올바르게 수정된다.

---

## 현재 상태 분석

### 에셋 파일 현황 (`Assets/_Project/Resources/Config/`)

| 파일 | 상태 | 비고 |
|------|------|------|
| AIScenarioConfig_Human_A.asset | 존재, 데이터 정상 | Human 물량형 ✓ |
| AIScenarioConfig_Human_B.asset | 존재, **데이터 오류** | Human_A와 동일 데이터 (테크형 아님) |
| AIScenarioConfig_Human_C.asset | 존재, **데이터 오류** | Human_A와 동일 데이터 (균형형 아님) |
| AIScenarioConfig_Spirit_*.asset | **미존재** | — |
| AIScenarioConfig_Transcendence_*.asset | **미존재** | — |

### AIScenarioConfig.cs 구조
- `string scenarioName` — 이름
- `List<BuildOrderStep> _steps` — 단일 시나리오의 빌드오더 목록
- `IReadOnlyList<BuildOrderStep> Steps` → `_steps` 반환

**1개 파일 = 1개 시나리오** 구조. 3개 시나리오를 담으려면 코드 변경 필요.

### AIOpponentController.cs 의존성
- 생성자: `AIScenarioConfig scenario` 파라미터로 수신
- 내부: `private readonly AIScenarioConfig _scenario`
- 사용: `foreach (var step in _scenario.Steps)` — Steps만 접근

### GameBootstrapper.Setup.cs
- `LoadRandomHumanScenario()`: `Human_A/B/C` 3개 파일을 모두 로드해 목록 구성 후 랜덤 선택
- `InitializeAI()`: 선택된 `AIScenarioConfig`를 `AIOpponentController`에 주입
- 주석에 "Human 종족만 시나리오 보유 — 추후 종족별 분기 추가 예정"

### BuildingType / UnitType enum 정수값 (YAML 작성에 필요)

**AIActionType:**
- PlaceBuilding = 0 / UpgradeBuilding = 1 / StartProduction = 2

**phaseIndex:**
- Phase 1 = 0 / Phase 2 = 1 / Phase 3 = 2 / Phase 4 = 3

**Human 건물 (BuildingType):**
- TrainingCamp=7, WarAcademy=8, HumanBarracks=9
- Gunsmith=10, Armory=11, WeaponForge=12
- Garage=13, VehicleBay=14

**Human 유닛 (UnitType):**
- Pistoleer=0, Assault=1, Sniper=2, LittleKnight=3, SpearMan=4, BattleAxe=5, Tank=6, CannonCart=7

**Spirit 건물 (BuildingType):**
- FireSpire=15, BlazeConduit=16, InfernoCore=17
- AquaSpring=18, TidalNexus=19, OceanicHeart=20
- StoneMound=21, TerraForge=22, GaeaSanctum=23

**Spirit 유닛 (UnitType):**
- FlameSpirit=10, EmberSpirit=11, InfernoSpirit=12
- DustSpirit=13, BoulderSpirit=14, QuakeSpirit=15
- TideSpirit=16, StreamSpirit=17, TorrentSpirit=18

**Transcendence 건물 (BuildingType):**
- PrimalAltar=24, PrimalDen=25, PrimalSanctuary=26
- FeralAltar=27, FeralDen=28, FeralSanctuary=29
- SporePatch=30, FloralNursery=31

**Transcendence 유닛 (UnitType):**
- BearGuard=20, FoxMagician=21, LionKnight=22, RhinoBreaker=23
- EagleArcher=24, RabbitTrickster=25, MushroomBomber=26, BloomFairy=27

### 기존 에셋 GUID (신규 에셋 작성 참조용)
- AIScenarioConfig.cs 스크립트 GUID: `f212b89a59f47d54ea2b03316a42850b`
- m_EditorClassIdentifier: `Assembly-CSharp::Hexiege.Infrastructure.AIScenarioConfig`

---

## 영향 범위

| 파일 | 변경 유형 | 사유 |
|------|----------|------|
| `AIScenarioConfig.cs` | 수정 | ScenarioBundle 클래스 추가 + scenarios 필드 추가 |
| `AIOpponentController.cs` | 수정 | AIScenarioConfig → IReadOnlyList<BuildOrderStep> + string으로 파라미터 변경 |
| `GameBootstrapper.Setup.cs` | 수정 | LoadRandomHumanScenario() → 새 구조 로드 방식으로 교체 |
| `AIScenarioConfig_Human.asset` (신규) | 생성 | 3개 Human 시나리오 포함 |
| `AIScenarioConfig_Spirit.asset` (신규) | 생성 | 3개 Spirit 시나리오 포함 |
| `AIScenarioConfig_Transcendence.asset` (신규) | 생성 | 3개 Transcendence 시나리오 포함 |
| `AIScenarioConfig_Human_A/B/C.asset` | 주석 처리 후 삭제 예정 | 신규 구조로 대체 |

---

## 발견된 부가 이슈

**Human_B/C 빌드오더 데이터 오류**: 기존 AIConfigSetup.cs 에디터 스크립트가 Human_A 데이터를 B/C에도 동일 적용. 이번 작업에서 올바른 데이터로 수정됨.

| 에셋 | 기대 Phase 2 시작 | 실제 Phase 2 시작 |
|------|------------------|------------------|
| Human_B (테크형) | UpgradeBuilding WarAcademy | PlaceBuilding Gunsmith ← 오류 |
| Human_C (균형형) | UpgradeBuilding WarAcademy | PlaceBuilding Gunsmith ← 오류 |
