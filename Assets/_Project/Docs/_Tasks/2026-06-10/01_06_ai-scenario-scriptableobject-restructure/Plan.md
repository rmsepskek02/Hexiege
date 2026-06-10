# Plan — AI 시나리오 ScriptableObject 구조 개편

## 작업 개요

AI 시나리오 에셋을 "종족당 1파일, 파일 안에 3개 시나리오"로 재구성한다.
코드 변경 3개 파일, 신규 에셋 3개 파일, 구 에셋 3개 삭제(테스트 후)가 전부다.

> ⚠️ 기존 로직 제거 규칙: `LoadRandomHumanScenario()` 메서드와 Human_A/B/C.asset은
> 테스트 통과 후(Step 7)에 최종 삭제한다. 구현 단계에서는 주석 처리로만 비활성화.

---

## Step 1. `AIScenarioConfig.cs` 수정

**GameSystemRules_AI.md 규칙 11 근거**: 시나리오는 ScriptableObject로 직렬화해 Inspector에서 편집 가능해야 한다.

**변경 내용:**

1. `[System.Serializable] public class ScenarioBundle` 신규 추가 (namespace 내부):
   ```csharp
   // 필드:
   public string scenarioName = "Unnamed";
   public List<BuildOrderStep> steps = new List<BuildOrderStep>();
   ```

2. `AIScenarioConfig` 클래스에 신규 필드 추가:
   ```csharp
   public List<ScenarioBundle> scenarios = new List<ScenarioBundle>();
   ```

3. 기존 `scenarioName`, `_steps`, `Steps` 필드/프로퍼티는 **레거시 표기 주석** 추가 후 유지
   (Human_A/B/C 에셋이 참조 중이므로 삭제 불가 — 에셋 삭제 후 제거 가능)

4. 파일 헤더 주석 갱신: "종족별 A/B/C 에셋" → "종족별 단일 에셋(3개 시나리오 포함)" 방식 설명

---

## Step 2. `AIOpponentController.cs` 수정

**GameSystemRules_AI.md 규칙 28 근거**: AIOpponentController는 UseCase를 통해서만 게임 상태를 변경한다. ScriptableObject 직접 참조 최소화.

**변경 내용:**

현재:
```csharp
private readonly AIScenarioConfig _scenario;
// 생성자 파라미터: AIScenarioConfig scenario
// 사용: foreach (var step in _scenario.Steps)
```

변경 후:
```csharp
private readonly IReadOnlyList<BuildOrderStep> _scenarioSteps;
private readonly string _scenarioName;
// 생성자 파라미터: IReadOnlyList<BuildOrderStep> scenarioSteps, string scenarioName
// 사용: foreach (var step in _scenarioSteps)
```

GameBootstrapper의 `scenario.scenarioName` 참조 로그도 함께 수정.

---

## Step 3. `GameBootstrapper.Setup.cs` 수정

**변경 내용:**

1. `LoadRandomHumanScenario()` 메서드를 **주석 처리** (비활성화).

2. 새 메서드 `LoadScenarioBundle()` 추가:
   ```csharp
   // Config/AIScenarioConfig_Human 로드 → scenarios[Random.Range(0,3)] 반환
   // 에셋 없으면 null 반환 (에러 로그 출력)
   ```

3. `InitializeAI()` 내 시나리오 로드 부분 교체:
   ```csharp
   // 구: AIScenarioConfig scenario = LoadRandomHumanScenario();
   // 신: (IReadOnlyList<BuildOrderStep> steps, string name) = LoadScenarioBundle();
   ```

4. `AIOpponentController` 생성자 호출부 파라미터 수정 (scenario → steps + name).

---

## Step 4. `AIScenarioConfig_Human.asset` 생성

YAML 직접 작성. 3개 시나리오 포함.

**scenarios[0] — Human_A (물량형):**
- Phase 1: PlaceBuilding TrainingCamp(7) L0 D5, StartProd LittleKnight(3) L0 D20
- Phase 2: PlaceBuilding Gunsmith(10) L1 D20, StartProd Pistoleer(0) L1 D35
- Phase 3: Upgrade WarAcademy(8) L0 D5, StartProd SpearMan(4) L0 D30, Upgrade Armory(11) L1 D60, StartProd Assault(1) L1 D90
- Phase 4: Upgrade HumanBarracks(9) L0 D15, StartProd BattleAxe(5) L0 D45, Upgrade WeaponForge(12) L1 D50, StartProd Sniper(2) L1 D80

**scenarios[1] — Human_B (테크형):**
- Phase 1: PlaceBuilding TrainingCamp(7) L0 D5, StartProd LittleKnight(3) L0 D20
- Phase 2: Upgrade WarAcademy(8) L0 D20, StartProd SpearMan(4) L0 D45, PlaceBuilding Gunsmith(10) L1 D60, StartProd Pistoleer(0) L1 D75
- Phase 3: Upgrade HumanBarracks(9) L0 D5, StartProd BattleAxe(5) L0 D35, Upgrade Armory(11) L1 D60, StartProd Assault(1) L1 D90
- Phase 4: PlaceBuilding Garage(13) L2 D15, StartProd CannonCart(7) L2 D30, Upgrade VehicleBay(14) L2 D60, StartProd Tank(6) L2 D90

**scenarios[2] — Human_C (균형형):**
- Phase 1: PlaceBuilding TrainingCamp(7) L0 D5, StartProd LittleKnight(3) L0 D20
- Phase 2: Upgrade WarAcademy(8) L0 D20, StartProd SpearMan(4) L0 D45, PlaceBuilding Gunsmith(10) L1 D60, StartProd Pistoleer(0) L1 D75
- Phase 3: Upgrade HumanBarracks(9) L0 D5, StartProd BattleAxe(5) L0 D35, Upgrade Armory(11) L1 D60, StartProd Assault(1) L1 D90
- Phase 4: Upgrade WeaponForge(12) L1 D15, StartProd Sniper(2) L1 D45, PlaceBuilding Garage(13) L2 D50, StartProd CannonCart(7) L2 D65, Upgrade VehicleBay(14) L2 D120, StartProd Tank(6) L2 D150

---

## Step 5. `AIScenarioConfig_Spirit.asset` 생성

**scenarios[0] — Spirit-Inferno (불 집중형):**
- Phase 1: Place FireSpire(15) L0 D5, StartProd EmberSpirit(11) L0 D20
- Phase 2: Upgrade BlazeConduit(16) L0 D20, StartProd FlameSpirit(10) L0 D50, Place StoneMound(21) L2 D70, StartProd DustSpirit(13) L2 D90
- Phase 3: Upgrade InfernoCore(17) L0 D15, StartProd InfernoSpirit(12) L0 D50, Upgrade TerraForge(22) L2 D60, StartProd BoulderSpirit(14) L2 D90
- Phase 4: Upgrade GaeaSanctum(23) L2 D30, StartProd QuakeSpirit(15) L2 D70

**scenarios[1] — Spirit-Torrent (물 집중형):**
- Phase 1: Place AquaSpring(18) L1 D5, StartProd TideSpirit(16) L1 D20
- Phase 2: Upgrade TidalNexus(19) L1 D20, StartProd StreamSpirit(17) L1 D50, Place FireSpire(15) L0 D70, StartProd EmberSpirit(11) L0 D90
- Phase 3: Upgrade OceanicHeart(20) L1 D15, StartProd TorrentSpirit(18) L1 D50, Upgrade BlazeConduit(16) L0 D60, StartProd FlameSpirit(10) L0 D90
- Phase 4: Upgrade InfernoCore(17) L0 D30, StartProd InfernoSpirit(12) L0 D65

**scenarios[2] — Spirit-Quake (땅 집중형):**
- Phase 1: Place StoneMound(21) L2 D5, StartProd DustSpirit(13) L2 D20
- Phase 2: Upgrade TerraForge(22) L2 D20, StartProd BoulderSpirit(14) L2 D50, Place AquaSpring(18) L1 D70, StartProd TideSpirit(16) L1 D90
- Phase 3: Upgrade GaeaSanctum(23) L2 D15, StartProd QuakeSpirit(15) L2 D50, Upgrade TidalNexus(19) L1 D60, StartProd StreamSpirit(17) L1 D90
- Phase 4: Upgrade OceanicHeart(20) L1 D30, StartProd TorrentSpirit(18) L1 D65

---

## Step 6. `AIScenarioConfig_Transcendence.asset` 생성

**scenarios[0] — Trans-Rush (초반 물량형):**
- Phase 1: Place PrimalAltar(24) L0 D5, StartProd RabbitTrickster(25) L0 D20
- Phase 2: Place FeralAltar(27) L1 D15, StartProd FoxMagician(21) L1 D35
- Phase 3: Upgrade PrimalDen(25) L0 D10, StartProd RhinoBreaker(23) L0 D35, Upgrade FeralDen(28) L1 D50, StartProd EagleArcher(24) L1 D75
- Phase 4: Upgrade FeralSanctuary(29) L1 D20, StartProd LionKnight(22) L1 D55

**scenarios[1] — Trans-Flora (동물A + 식물 균형형):**
- Phase 1: Place PrimalAltar(24) L0 D5, StartProd RabbitTrickster(25) L0 D20
- Phase 2: Upgrade PrimalDen(25) L0 D20, StartProd RhinoBreaker(23) L0 D45, Place SporePatch(30) L2 D70, StartProd MushroomBomber(26) L2 D90
- Phase 3: Upgrade PrimalSanctuary(26) L0 D10, StartProd BearGuard(20) L0 D55, Upgrade FloralNursery(31) L2 D65, StartProd BloomFairy(27) L2 D90
- Phase 4: Place FeralAltar(27) L1 D15, StartProd FoxMagician(21) L1 D35

**scenarios[2] — Trans-Beast (동물 고테크형):**
- Phase 1: Place FeralAltar(27) L1 D5, StartProd FoxMagician(21) L1 D20
- Phase 2: Upgrade FeralDen(28) L1 D20, StartProd EagleArcher(24) L1 D45, Place PrimalAltar(24) L0 D70, StartProd RabbitTrickster(25) L0 D90
- Phase 3: Upgrade FeralSanctuary(29) L1 D10, StartProd LionKnight(22) L1 D40, Upgrade PrimalDen(25) L0 D60, StartProd RhinoBreaker(23) L0 D85
- Phase 4: Upgrade PrimalSanctuary(26) L0 D15, StartProd BearGuard(20) L0 D60

---

## Step 7. 구 에셋 정리 (테스트 통과 후)

테스트 통과 확인 후:
- `AIScenarioConfig_Human_A.asset` + `.meta` 삭제
- `AIScenarioConfig_Human_B.asset` + `.meta` 삭제
- `AIScenarioConfig_Human_C.asset` + `.meta` 삭제
- `GameBootstrapper`의 주석 처리된 `LoadRandomHumanScenario()` 코드 삭제

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| YAML 필드명이 C# 직렬화명과 불일치 → Unity가 데이터 무시 | ScenarioBundle 필드명을 public으로 해서 직렬화명 = 필드명 보장 |
| 신규 GUID 충돌 | 프리픽스 다른 고유값 사용 |
| AIOpponentController 생성자 변경 → 컴파일 에러 | GameBootstrapper 호출부도 동시에 수정 |
| Human_B/C 데이터 오류 수정 → 이전 오류 동작에 의존하는 테스트 실패 | 기존 B/C 데이터가 사실상 A와 동일했으므로 기능 차이 없음 |
