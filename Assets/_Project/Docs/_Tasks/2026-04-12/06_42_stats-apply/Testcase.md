# Testcase: 유닛/건물 스탯 적용 및 UI 표기

**작업일:** 2026-04-12  
**기준 문서:** `Assets/_Project/Docs/StatsReference.md`

---

## TC 목록

### TC-SINGLE-01: Pistoleer 이동 속도 확인

**전제:** 싱글플레이 또는 에디터 테스트. 인간계 팀이 게임에 진입한 상태.

**동작:**
1. 권총병(Pistoleer)을 생산한다.
2. 생산된 권총병이 적 방향으로 이동하는 것을 관찰한다.

**기댓값:**
- 권총병이 기존보다 느리게 이동한다. (이동속도 1.0 → 0.5로 절반 감소)
- 돌격소총병(Assault)보다 명확히 느린 속도로 이동한다.

**결과:** (테스트 후 기록)

---

### TC-SINGLE-02: FlameSpirit HP/공격력 확인

**전제:** 정령계 팀이 게임에 진입한 상태.

**동작:**
1. FlameSpirit를 생산한다.
2. FlameSpirit가 적 유닛(예: Pistoleer HP 30)을 공격한다.
3. 적 유닛이 한 번 공격받은 뒤 HP를 확인한다.

**기댓값:**
- FlameSpirit의 최대 HP는 50이다. (기존 플레이스홀더 30에서 증가)
- 적 유닛이 공격 1회에 2 데미지만 받는다. (기존 플레이스홀더 5에서 감소)

**결과:** (테스트 후 기록)

---

### TC-SINGLE-03: InfernoSpirit 강함 확인 (HP 100 / 공격력 25)

**전제:** 정령계 팀이 게임에 진입한 상태.

**동작:**
1. InfernoSpirit를 생산한다.
2. InfernoSpirit가 적 유닛을 공격한다.
3. 적 유닛이 한 번 공격받은 뒤 HP를 확인한다.

**기댓값:**
- InfernoSpirit의 최대 HP는 100이다. (가장 높은 정령계 유닛)
- 적 유닛이 공격 1회에 25 데미지를 받는다. (Pistoleer HP 30 → 한 방에 제거 가능)

**결과:** (테스트 후 기록)

---

### TC-SINGLE-04: BearGuard 탱커 확인 (HP 200)

**전제:** 초월계 팀이 게임에 진입한 상태.

**동작:**
1. BearGuard를 생산한다.
2. 적 유닛(예: Sniper 공격력 10)에게 공격받게 한다.

**기댓값:**
- BearGuard의 최대 HP는 200이다.
- 적 Sniper에게 20회 공격받아야 사망한다.

**결과:** (테스트 후 기록)

---

### TC-SINGLE-05: 초월계 건물 HP 확인

**전제:** 초월계 팀이 게임에 진입한 상태.

**동작:**
1. 게임 시작 시 자동 배치된 ElderTree(본기지)를 확인한다.
2. 적 유닛이 ElderTree를 공격하여 데미지를 입힌다.
3. FungalNode(배럭)를 건설하고 적 공격을 받게 한다.
4. HunterPlant(채굴소)를 건설하고 적 공격을 받게 한다.

**기댓값:**
- ElderTree 최대 HP = 200 (인간계 Castle 100의 2배)
- FungalNode 최대 HP = 50 (인간계 Barracks 30보다 높음)
- HunterPlant 최대 HP = 40 (인간계 MiningPost 20의 2배)

**결과:** (테스트 후 기록)

---

### TC-SINGLE-06: 인간계/정령계 건물 HP 동일 확인

**전제:** 인간계 또는 정령계 팀이 게임에 진입한 상태.

**동작:**
1. 인간계 Castle과 정령계 SpiritNexus 각각에 적 공격을 동일하게 가한다.
2. 배럭/채굴소 계열도 동일하게 확인한다.

**기댓값:**
- Castle/SpiritNexus 모두 HP 100으로 동일하다.
- Barracks/SummoningAltar 모두 HP 30으로 동일하다.
- MiningPost/ManaRift 모두 HP 20으로 동일하다.

**결과:** (테스트 후 기록)

---

### TC-SINGLE-07: Spirit/Transcendence 생산 비용 확인

**전제:** 정령계 또는 초월계 팀이 게임에 진입한 상태.

**동작:**
1. 생산 패널을 열어 각 유닛 버튼에 표시된 골드 비용 텍스트를 확인한다.
2. 각 유닛을 실제로 생산하여 골드가 올바르게 차감되는지 확인한다.

**기댓값 (정령계):**
- EmberSpirit: 50G 표시 및 차감
- FlameSpirit: 200G 표시 및 차감
- InfernoSpirit: 500G 표시 및 차감

**기댓값 (초월계):**
- FoxMagician: 50G 표시 및 차감
- LionKnight: 200G 표시 및 차감
- BearGuard: 400G 표시 및 차감

**결과:** (테스트 후 기록)

---

### TC-SINGLE-08: 생산 패널 골드 비용 텍스트 표기 확인

**전제:** 게임에 진입한 상태. 배럭을 클릭하여 생산 패널 열기.

**동작:**
1. 배럭을 클릭하여 생산 패널을 연다.
2. 유닛 버튼 3개에 골드 비용 텍스트가 표시되는지 확인한다.
3. 다른 종족의 배럭도 동일하게 확인한다.

**기댓값:**
- 각 유닛 버튼 아래/위에 "50G", "100G" 등 골드 비용 텍스트가 표시된다.
- 종족이 다르면 버튼 텍스트 값도 다르게 표시된다 (예: 정령계 EmberSpirit=50G, InfernoSpirit=500G).
- Inspector에서 `_slot1CostText`, `_slot2CostText`, `_slot3CostText`가 연결되지 않은 경우 텍스트가 표시되지 않는다 (null 안전 처리 확인).

**결과:** (테스트 후 기록)

---

### TC-SINGLE-09: 건물 배치 팝업 HP/비용 텍스트 확인

**전제:** 게임에 진입한 상태. 빈 타일을 탭하여 건물 배치 팝업 열기.

**동작:**
1. 자기 팀의 빈 타일을 탭하여 건물 배치 팝업을 연다.
2. 배럭 버튼과 채굴소 버튼에 HP 및 골드 비용 텍스트가 표시되는지 확인한다.
3. 초월계 팀의 빈 타일에서도 동일하게 확인한다.

**기댓값:**
- 인간계/정령계: 배럭 HP=30, 비용=100G / 채굴소 HP=20, 비용=50G
- 초월계: 배럭(FungalNode) HP=50, 비용=100G / 채굴소(HunterPlant) HP=40, 비용=50G
- Inspector에서 텍스트 필드가 연결되지 않은 경우 텍스트 미표시 (null 안전 처리 확인).

**결과:** (테스트 후 기록)

---

### TC-SINGLE-10: Spirit/Transcendence 생산 시간 확인

**전제:** 정령계 또는 초월계 팀이 게임에 진입한 상태. 500골드 이상 보유.

**동작:**
1. InfernoSpirit(30초)를 생산 큐에 추가하고 생산 진행률 바를 관찰한다.
2. BearGuard(25초)를 생산 큐에 추가하고 동일하게 관찰한다.

**기댓값:**
- InfernoSpirit 생산 완료까지 약 30초 소요된다.
- BearGuard 생산 완료까지 약 25초 소요된다.
- (기존 플레이스홀더 5초/10초/15초에서 변경)

**결과:** (테스트 후 기록)

---

### TC-MULTI-01: 멀티플레이 초월계 건물 HP 동기화

**전제:** Host(Blue팀=초월계) + Client(Red팀=인간계) 구성.

**동작:**
1. Host의 ElderTree(본기지)에 Client의 유닛이 공격한다.
2. Host와 Client 양쪽에서 ElderTree HP를 관찰한다.

**기댓값:**
- Host와 Client 양측에서 ElderTree 최대 HP = 200으로 동일하게 표시된다.

**결과:** (에이전트 실기 불가 — 사용자 확인 필요)

---

## QA 섹션 (qa-tester 에이전트 전용)

### 변경된 파일 목록
1. `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`
2. `Assets/_Project/Scripts/Domain/Unit/UnitProductionStats.cs`
3. `Assets/_Project/Scripts/Domain/Building/BuildingStats.cs`
4. `Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs`
5. `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
6. `Assets/_Project/Scripts/Infrastructure/Network/NetworkBuildingController.cs`
7. `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`
8. `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

### 정적 분석 체크포인트
- `BuildingStats.GetMaxHp(BuildingType)` 기존 호출부 모두 `GetMaxHp(type, RaceId.Human)` 위임인지 확인
- `BuildingPlacementUseCase.PlaceBuilding()` 호출부 중 `RaceId` 미전달 누락 없는지 확인
- `PlaceMiningPost()`, `PlaceMiningPostDirect()` 등 모든 오버로드에 `RaceId` 파라미터 추가됐는지 확인
- `ProductionPanelUI._slot1/2/3CostText` null 체크(`?.SetText`) 포함 여부
- `BuildingPlacementUI._barracks/miningPost HpText/CostText` null 체크 포함 여부
- Application 레이어(`BuildingPlacementUseCase.cs`)에 `GameRaceContext` 직접 import 없는지 확인 (레이어 위반)
- LionKnight 주석에 "2히트"로 수정됐는지 확인 (GetAttackCooldown, GetHitFrameTime 모두)

---

## 정적 분석 결과 (qa-tester)

**분석일:** 2026-04-12  
**분석 파일:** 변경된 파일 8개 전체 + 연관 호출부 확인

---

### 체크포인트 1: 스탯 값 정확성

| 유닛 | 항목 | 기준값 | 코드값 | 파일/라인 | 판정 |
|------|------|--------|--------|-----------|------|
| Pistoleer | 이동속도 | 0.5f | 0.5f | UnitStats.cs:84 | PASS |
| FlameSpirit | HP | 50 | 50 | UnitStats.cs:31 | PASS |
| FlameSpirit | ATK | 2 | 2 | UnitStats.cs:49 | PASS |
| InfernoSpirit | HP | 100 | 100 | UnitStats.cs:32 | PASS |
| InfernoSpirit | ATK | 25 | 25 | UnitStats.cs:50 | PASS |
| BearGuard | HP | 200 | 200 | UnitStats.cs:35 | PASS |
| BearGuard | ATK | 10 | 10 | UnitStats.cs:53 | PASS |
| FoxMagician | HP | 20 | 20 | UnitStats.cs:34 | PASS |
| FoxMagician | ATK | 8 | 8 | UnitStats.cs:52 | PASS |
| LionKnight | HP | 50 | 50 | UnitStats.cs:36 | PASS |
| LionKnight | ATK | 9 | 9 | UnitStats.cs:54 | PASS |
| EmberSpirit | 생산시간 | 5초 | 5f | UnitProductionStats.cs:29 | PASS |
| EmberSpirit | 비용 | 50G | 50 | UnitProductionStats.cs:47 | PASS |
| FlameSpirit | 생산시간 | 15초 | 15f | UnitProductionStats.cs:30 | PASS |
| FlameSpirit | 비용 | 200G | 200 | UnitProductionStats.cs:48 | PASS |
| InfernoSpirit | 생산시간 | 30초 | 30f | UnitProductionStats.cs:31 | PASS |
| InfernoSpirit | 비용 | 500G | 500 | UnitProductionStats.cs:49 | PASS |
| FoxMagician | 생산시간 | 5초 | 5f | UnitProductionStats.cs:33 | PASS |
| FoxMagician | 비용 | 50G | 50 | UnitProductionStats.cs:51 | PASS |
| BearGuard | 생산시간 | 25초 | 25f | UnitProductionStats.cs:34 | PASS |
| BearGuard | 비용 | 400G | 400 | UnitProductionStats.cs:52 | PASS |

**판정: PASS**

---

### 체크포인트 2: BuildingStats 종족별 HP

| 건물 | 종족 | 기준값 | 코드값 | 파일/라인 | 판정 |
|------|------|--------|--------|-----------|------|
| Castle | Transcendence | 200 | 200 | BuildingStats.cs:34 | PASS |
| Castle | 나머지(_) | 100 | 100 | BuildingStats.cs:35 | PASS |
| Barracks | Transcendence | 50 | 50 | BuildingStats.cs:37 | PASS |
| Barracks | 나머지(_) | 30 | 30 | BuildingStats.cs:38 | PASS |
| MiningPost | Transcendence | 40 | 40 | BuildingStats.cs:40 | PASS |
| MiningPost | 나머지(_) | 20 | 20 | BuildingStats.cs:41 | PASS |
| GetMaxHp(type) 단일 | 하위 호환 | Human 위임 | GetMaxHp(type, RaceId.Human) | BuildingStats.cs:21 | PASS |

**판정: PASS**

---

### 체크포인트 3: 레이어 위반 검사

`BuildingPlacementUseCase.cs` using 선언:
- `using Hexiege.Domain;` 만 존재 (19번 줄)
- `using Hexiege.Infrastructure;` 없음
- `GameRaceContext` 참조 없음

**판정: PASS** — Application 레이어 위반 없음

---

### 체크포인트 4: 호출부 완전성 (RaceId 파라미터)

| 메서드 | RaceId 파라미터 | 호출부 전달 | 판정 |
|--------|----------------|-------------|------|
| PlaceBuilding | 추가됨 (기본값 Human) | GameBootstrapper, BuildingPlacementUI, NetworkBuildingController 모두 전달 | PASS |
| PlaceMiningPost (private) | 추가됨 (기본값 Human) | PlaceBuilding 내부에서 race 전달 | PASS |
| PlaceMiningPostDirect | 추가됨 (기본값 Human) | GameBootstrapper에서 BlueRace/RedRace 전달 | PASS |
| PlaceBuildingWithId | 추가됨 (기본값 Human) | NetworkBuildingController에서 BlueRace/RedRace 전달 | PASS |

**판정: PASS**

---

### 체크포인트 5: UI null 안전성

**ProductionPanelUI — CostText:**
- `_slot1CostText`: `if (_slot1CostText != null)` 체크 후 `SetText()` (ProductionPanelUI.cs:949)
- `_slot2CostText`: `if (_slot2CostText != null)` 체크 후 `SetText()` (ProductionPanelUI.cs:951)
- `_slot3CostText`: `if (_slot3CostText != null)` 체크 후 `SetText()` (ProductionPanelUI.cs:953)

**BuildingPlacementUI — 스탯 텍스트:**
- `_barracksHpText`: `if (_barracksHpText != null)` 체크 후 `SetText()` (BuildingPlacementUI.cs:280)
- `_barracksCostText`: `if (_barracksCostText != null && _config != null)` 체크 후 `SetText()` (BuildingPlacementUI.cs:282)
- `_miningPostHpText`: `if (_miningPostHpText != null)` 체크 후 `SetText()` (BuildingPlacementUI.cs:286)
- `_miningPostCostText`: `if (_miningPostCostText != null && _config != null)` 체크 후 `SetText()` (BuildingPlacementUI.cs:289)

**판정: PASS** — 전체 null 체크 완비

---

### 체크포인트 6: LionKnight "2히트" 주석

| 메서드 | 라인 | 주석 내용 | 판정 |
|--------|------|-----------|------|
| GetAttackPower | UnitStats.cs:54 | `// 2히트 공격 (총 데미지 = 9 × 2 = 18)` | PASS |
| GetAttackCooldown | UnitStats.cs:118 | `// 2:20 = 2.33초 (2히트 공격)` | PASS |
| GetHitFrameTime | UnitStats.cs:146 | `// 0:15 = 0.250초 (Plan.md 확정값)` — "2히트" 문구 없음 | FAIL |

**BUG-STATS-01 (Minor)**
- **위치:** `UnitStats.cs` 146번 줄 `GetHitFrameTime` LionKnight case
- **내용:** GetAttackCooldown에는 "(2히트 공격)" 주석이 있지만, GetHitFrameTime에는 "(Plan.md 확정값)"만 기재되어 있어 동일 기준으로 관리되지 않음
- **기준:** 체크포인트 6 — "GetAttackCooldown, GetHitFrameTime 두 곳 모두" 확인 요구
- **제안 수정:** `// 0:15 = 0.250초 (2히트 공격 — 첫 번째 히트 프레임)`

---

### 체크포인트 7: 플레이스홀더 주석 제거

- `UnitStats.cs`: "(미정 → 플레이스홀더)" 문자열 검색 결과 없음
- `UnitProductionStats.cs`: "(미정 → 플레이스홀더)" 문자열 검색 결과 없음

**판정: PASS** — 플레이스홀더 주석 전부 제거됨

---

### 추가 발견 사항 (Minor)

**주석 불일치 (Minor, 기능 영향 없음)**
- **위치:** `ProductionPanelUI.cs` 108번 줄 Tooltip
- **내용:** `"Blue팀 Spirit 종족 초상화 (slot1=FlameSpirit, slot2=EmberSpirit, slot3=InfernoSpirit)"` 이라고 명시되어 있지만, `BindButtonUnitTypes` (926번 줄)에서 실제 순서는 slot0=EmberSpirit, slot1=FlameSpirit
- **판정:** 기능 오작동 없음. Tooltip 주석이 실제 코드 순서와 불일치. 혼동 방지를 위해 주석 수정 권장

---

### 종합 판정

| 체크포인트 | 판정 |
|------------|------|
| 1. 스탯 값 정확성 | PASS |
| 2. BuildingStats 종족별 HP | PASS |
| 3. 레이어 위반 검사 | PASS |
| 4. 호출부 완전성 (RaceId) | PASS |
| 5. UI null 안전성 | PASS |
| 6. LionKnight "2히트" 주석 | FAIL (Minor) |
| 7. 플레이스홀더 주석 제거 | PASS |

**전체 판정: CONDITIONAL PASS**

BUG-STATS-01 (Minor) 1건 발견. 주석 누락으로 기능 오동작은 없음. 해당 주석 1줄 수정 후 실기 테스트 진행 가능.
