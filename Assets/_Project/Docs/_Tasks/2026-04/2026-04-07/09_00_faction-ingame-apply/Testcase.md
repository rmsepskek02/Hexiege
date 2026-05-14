# Testcase: 종족 선택 → 인게임 적용

**작업일**: 2026-04-07  
**작업명**: faction-ingame-apply

---

## 테스트 케이스

### SINGLE-01: Human 선택 → 인게임 Human 유닛 스폰

**전제:** 로비에서 Human 종족을 선택한 상태

**동작:**
1. 로비 종족 선택 화면에서 Human을 선택
2. 배틀 시작 버튼을 눌러 게임 씬으로 진입
3. 게임 씬에서 건물(Barracks)를 클릭해 유닛 생산

**기댓값:**
- 스폰된 유닛의 외형이 Human 유닛(군인 계열)으로 표시됨
- Blue 팀은 Human Blue 프리팹, Red 팀은 Human Red 프리팹

**결과:** PASS (2026-04-07 사용자 실기 확인)

---

### SINGLE-02: Spirit 선택 → 인게임 Spirit 유닛 스폰

**전제:** 로비에서 Spirit 종족을 선택한 상태

**동작:**
1. 로비 종족 선택 화면에서 Spirit을 선택
2. 배틀 시작 버튼을 눌러 게임 씬으로 진입
3. 건물을 클릭해 유닛 생산

**기댓값:**
- 스폰된 유닛의 외형이 Spirit 유닛(정령 계열)으로 표시됨
- Human 유닛이 나오면 실패

**결과:** PASS (2026-04-07 사용자 실기 확인)

---

### SINGLE-03: Transcendence 선택 → 인게임 Transcendence 유닛 스폰

**전제:** 로비에서 Transcendence 종족을 선택한 상태

**동작:**
1. 로비 종족 선택 화면에서 Transcendence를 선택
2. 배틀 시작 버튼을 눌러 게임 씬으로 진입
3. 건물을 클릭해 유닛 생산

**기댓값:**
- 스폰된 유닛의 외형이 Transcendence 유닛(동물 계열: 곰/여우/사자)으로 표시됨

**결과:** PASS (2026-04-07 사용자 실기 확인)

---

### SINGLE-04: 모든 유닛 타입에 종족 프리팹 적용

**전제:** Spirit 종족을 선택한 상태, 3종류 유닛이 모두 생산 가능한 상황

**동작:**
1. Spirit 선택 후 게임 진입
2. Pistoleer 역할 유닛 생산 (가장 저렴한 유닛)
3. Assault 역할 유닛 생산
4. Sniper 역할 유닛 생산

**기댓값:**
- Pistoleer 역할: EmberSpirit 외형으로 스폰
- Assault 역할: FlameSpirit 외형으로 스폰
- Sniper 역할: InfernoSpirit 외형으로 스폰

**결과:** PASS (2026-04-07 사용자 실기 확인)

---

### SINGLE-05: Spirit 선택 → 인게임 Spirit 건물 스폰

**전제:** 로비에서 Spirit 종족을 선택한 상태

**동작:**
1. 로비에서 Spirit 선택 후 게임 씬 진입
2. 배치 가능한 타일을 클릭해 Castle(본기지) 확인
3. Barracks(배럭) 건물 배치

**기댓값:**
- 게임 시작 시 본기지가 SpiritNexus 외형으로 표시됨
- 배럭 배치 시 SummoningAltar 외형으로 표시됨
- Human 건물(Castle/Barracks)이 나오면 실패

**결과:** PASS (2026-04-07 사용자 실기 확인)

---

### SINGLE-06: Transcendence 선택 → 인게임 Transcendence 건물 스폰

**전제:** 로비에서 Transcendence 종족을 선택한 상태

**동작:**
1. 로비에서 Transcendence 선택 후 게임 씬 진입
2. 본기지 외형 확인
3. 배럭 건물 배치

**기댓값:**
- 본기지가 ElderTree 외형으로 표시됨
- 배럭 배치 시 HunterPlant 외형으로 표시됨

**결과:** PASS (2026-04-07 사용자 실기 확인)

---

### MULTI-01: Host Spirit, Client Human — 각자 다른 종족 유닛 스폰

**전제:** Host가 Spirit, Client가 Human을 각자 선택한 상태

**동작:**
1. Host는 Spirit 선택, Client는 Human 선택
2. 매칭 후 게임 씬 진입
3. Host(Blue 팀)가 유닛 생산
4. Client(Red 팀)가 유닛 생산

**기댓값:**
- Host 화면: Blue 팀 = Spirit 유닛, Red 팀 = Human 유닛 표시
- Client 화면: Blue 팀 = Spirit 유닛, Red 팀 = Human 유닛 표시 (동일하게 동기화)

**결과:** PASS (2026-04-07 사용자 실기 확인)

---

## QA 섹션 (qa-tester 에이전트 전용)

### 정적 분석 결과 (2026-04-07)

---

#### 1. 컴파일 에러 여부

| 파일 | using 선언 | namespace | 타입 일치 | 판정 |
|------|-----------|-----------|----------|------|
| UnitFactory.cs | Hexiege.Domain / Core / Application / Presentation 모두 존재 | Hexiege.Infrastructure 정상 | UnitTeamPrefabSet 구조체 — pistoleer/assault/sniper 필드 소문자, SetupUnitFactoryPrefabs.cs에서 동일 이름 참조 | PASS |
| GameBootstrapper.cs | 변경 내용(GameRaceContext.Set 추가)에 필요한 using 이미 기존에 포함 | 이상 없음 | RaceId, LocalPlayerRace 모두 정상 타입 | PASS |
| SetupUnitFactoryPrefabs.cs | UnityEditor / UnityEngine / Hexiege.Infrastructure 정상 | 글로벌 namespace(public class, MenuItem 사용) — 에디터 전용이므로 무방 | SerializedObject.FindProperty 필드명과 UnitFactory 실제 필드명 일치 확인 완료 | PASS |

컴파일 에러 가능성: 없음.

---

#### 2. _bluePrefabs 제거 연쇄 영향 (프로젝트 전체 Grep)

프로젝트 전체 `.cs` 파일에서 `_bluePrefabs` / `_redPrefabs` 검색 결과:
- `BuildingFactory.cs` L50~51, L100 — BuildingFactory 자체 필드로 사용 중. UnitFactory와 무관. 영향 없음.
- UnitFactory.cs에서 해당 필드명 사용 흔적 없음 (완전 교체 확인).

연쇄 영향: 없음.

---

#### 3. GameRaceContext 초기화 타이밍

싱글플레이 진입 경로:

```
BattleViewModel.LoadSingleplayScene()
  → GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)  [Lobby 씬, 씬 전환 전]
  → SceneManager.LoadScene("Game")
  → GameBootstrapper.Start()
      → GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)  [Game 씬, LoadMap() 전]
      → LoadMap() → CreateUseCases() → UnitFactory.Awake() 이미 실행됨
```

- BattleViewModel과 GameBootstrapper 양쪽에서 동일 값으로 중복 호출됨.
- 두 호출 모두 `LocalPlayerRace.Current`와 `RaceId.Human`을 전달하므로 결과가 동일. 오동작 없음.
- UnitFactory.Awake()는 Game 씬 시작 시 이미 실행 완료 → GameBootstrapper.Start()의 GameRaceContext.Set()이 Load 전에 호출되므로 첫 스폰 전 종족 정보 준비 완료. 타이밍 이상 없음.

중복 Set 호출 자체는 Minor 수준 코드 정리 대상이나 동작 오류는 아님.

---

#### 4. 에디터 스크립트 정확성

에디터 스크립트가 참조하는 프리팹 경로와 실제 파일 존재 여부 대조:

| 종족 | 팀 | 역할 | 에디터 참조 경로 | 실제 파일 존재 |
|------|-----|------|----------------|--------------|
| Human | Blue | Pistoleer | .../Human/Unit_Pistoleer_Blue.prefab | 존재 |
| Human | Blue | Assault | .../Human/Unit_Assault_Blue.prefab | 존재 |
| Human | Blue | Sniper | .../Human/Unit_Sniper_Blue.prefab | 존재 |
| Human | Red | Pistoleer | .../Human/Unit_Pistoleer_Red.prefab | 존재 |
| Human | Red | Assault | .../Human/Unit_Assault_Red.prefab | 존재 |
| Human | Red | Sniper | .../Human/Unit_Sniper_Red.prefab | 존재 |
| Spirit | Blue | Pistoleer(EmberSpirit) | .../Spirit/Unit_EmberSpirit_Blue.prefab | 존재 |
| Spirit | Blue | Assault(FlameSpirit) | .../Spirit/Unit_FlameSpirit_Blue.prefab | 존재 |
| Spirit | Blue | Sniper(InfernoSpirit) | .../Spirit/Unit_InfernoSpirit_Blue.prefab | 존재 |
| Spirit | Red | Pistoleer(EmberSpirit) | .../Spirit/Unit_EmberSpirit_Red.prefab | 존재 |
| Spirit | Red | Assault(FlameSpirit) | .../Spirit/Unit_FlameSpirit_Red.prefab | 존재 |
| Spirit | Red | Sniper(InfernoSpirit) | .../Spirit/Unit_InfernoSpirit_Red.prefab | 존재 |
| Transcendence | Blue | Pistoleer(FoxMagician) | .../Transcendence/Unit_FoxMagician_Blue.prefab | 존재 |
| Transcendence | Blue | Assault(BearGuard) | .../Transcendence/Unit_BearGuard_Blue.prefab | 존재 |
| Transcendence | Blue | Sniper(LionKnight) | .../Transcendence/Unit_LionKnight_Blue.prefab | 존재 |
| Transcendence | Red | Pistoleer(FoxMagician) | .../Transcendence/Unit_FoxMagician_Red.prefab | 존재 |
| Transcendence | Red | Assault(BearGuard) | .../Transcendence/Unit_BearGuard_Red.prefab | 존재 |
| Transcendence | Red | Sniper(LionKnight) | .../Transcendence/Unit_LionKnight_Red.prefab | 존재 |

전체 18개 경로 모두 실제 파일 존재 확인. 에디터 스크립트 경로 이상 없음.

주의: Transcendence 폴더에 `BearShield_Blue/Red`, `FoxStaff_Blue/Red`, `LionSword_Blue/Red`라는 별도 파일도 있으나, 에디터 스크립트 및 UnitFactory는 `Unit_` 접두사 붙은 파일만 참조 → 충돌 없음.

---

#### 5. 멀티플레이 흐름 영향

- NetworkGameFlow.StartGameClientRpc()에서 `GameRaceContext.Set((RaceId)blueRace, (RaceId)redRace)` 호출 — 이 경로는 변경되지 않음.
- UnitFactory.CreateUnitObject()의 종족 분기 로직은 싱글/멀티 공용이므로 멀티플레이에서도 동일하게 동작.
- GameBootstrapper.Start()의 싱글플레이 분기(`!isNetworkMode`)는 네트워크 모드일 때 실행되지 않으므로 멀티플레이 흐름과 충돌 없음.

---

#### 발견된 버그 및 위험 요소

| 심각도 | 항목 | 설명 | 확인 방법 |
|--------|------|------|---------|
| Minor | GameRaceContext.Set() 중복 호출 | BattleViewModel.LoadSingleplayScene()과 GameBootstrapper.Start() 양쪽에서 동일 값으로 Set() 호출. 동작 오류는 없으나 코드 중복. | 정적 분석 확인 |
| 사용자 확인 필요 | Inspector 프리팹 연결 | 에디터 스크립트 미실행 시 Spirit/Transcendence 프리팹 필드 null → 유닛 미생성. 에디터 스크립트 실행 및 씬 저장 완료 여부 사용자 확인 필요 | 에디터에서 UnitFactory Inspector 직접 확인 |
| 사용자 확인 필요 | Spirit/Transcendence 프리팹 컴포넌트 | 멀티플레이에서 NetworkObject, NetworkUnit 컴포넌트 없으면 스폰 실패. 정적 분석으로 프리팹 내부 컴포넌트 확인 불가. | 실기 테스트 또는 에디터에서 프리팹 직접 확인 |

---

#### SINGLE-01~04 판정 근거

- 코드 로직 및 프리팹 경로 모두 이상 없음 → Inspector 프리팹 연결 완료 + 에디터 스크립트 실행 완료가 전제되어야 실기 통과 가능.
- 위 전제 충족 시 PASS 수렴 예상 → **CONDITIONAL PASS**.

#### MULTI-01 판정 근거

- 코드 분기 이상 없음. 에이전트 실기 불가 — 사용자 확인 필요.
- Spirit/Transcendence 프리팹의 NetworkObject/NetworkUnit 컴포넌트 탑재 여부 추가 확인 필요.
