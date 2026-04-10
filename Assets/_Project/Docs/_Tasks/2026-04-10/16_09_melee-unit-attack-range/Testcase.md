# Testcase: 유닛 타입 개편 + 근접 사거리 시스템

**날짜**: 2026-04-10

---

## TC 목록

### TC-1: 근접 유닛이 적에게 근접했을 때 공격을 시작한다
SINGLE-001

**전제:** 싱글플레이. 적 유닛이 아군 근접 유닛의 이동 경로 위에 있다.

**동작:**
1. 근접 유닛(FlameSpirit/EmberSpirit/BearGuard/LionKnight 중 하나)을 배치한다.
2. 근접 유닛이 적 유닛 방향으로 이동한다.
3. 이동 중 적 유닛과 가까워지는 것을 관찰한다.

**기댓값:**
- 근접 유닛이 적 유닛에 아주 가까이 접근했을 때(인접 타일 절반 이하 거리) 이동을 멈추고 공격 애니메이션을 시작한다.
- 적 유닛과 같은 타일에 완전히 겹치지 않는다.

**결과:** (사용자 실기 테스트 후 기록)

---

### TC-2: 원거리 유닛이 적에게 사거리 내에서 공격을 시작한다
SINGLE-002

**전제:** 싱글플레이. 기존 원거리 유닛(권총병, 저격병 등)이 정상 동작하는지 회귀 확인.

**동작:**
1. Human 권총병을 배치하고 이동시킨다.
2. 적 유닛이 1타일 거리에 들어오는 것을 관찰한다.

**기댓값:**
- 권총병이 인접 타일에서 공격을 시작한다. (기존과 동일)
- 근접 유닛에게 적용된 변경으로 권총병 동작이 달라지지 않는다.

**결과:** (사용자 실기 테스트 후 기록)

---

### TC-3: 근접 유닛이 적 건물에 근접했을 때 공격한다
SINGLE-003

**전제:** 싱글플레이. 근접 유닛이 적 본기지 방향으로 이동한다.

**동작:**
1. 근접 유닛을 생산하고 적 본기지 방향으로 이동시킨다.
2. 근접 유닛이 본기지에 접근하는 것을 관찰한다.

**기댓값:**
- 근접 유닛이 본기지 바로 앞(아주 가까운 거리)까지 이동한 뒤 공격을 시작한다.

**결과:** (사용자 실기 테스트 후 기록)

---

### TC-4: 근접 유닛이 적 유닛을 우회하지 않고 직진한다
SINGLE-004

**전제:** 싱글플레이. 아군 근접 유닛과 적 유닛이 정면으로 마주보는 상황.

**동작:**
1. 근접 유닛을 적 유닛 정면에 배치한다.
2. 근접 유닛이 이동을 시작한다.

**기댓값:**
- 근접 유닛이 적 유닛을 빙 돌아가지 않고 직진 경로로 접근한다.
- (기존에는 적 위치가 이동 차단 목록에 포함되어 우회 경로가 생성됐음)

**결과:** (사용자 실기 테스트 후 기록)

---

### TC-5: Spirit 종족 유닛들이 올바른 프리팹으로 생성된다
SINGLE-005

**전제:** 싱글플레이. Spirit 종족을 선택한 상태.

**동작:**
1. Spirit 종족으로 게임을 시작한다.
2. 배럭에서 유닛을 생산한다.

**기댓값:**
- Spirit 종족 유닛 프리팹(FlameSpirit, EmberSpirit, InfernoSpirit)이 올바르게 생성된다.
- Human 유닛 프리팹이 잘못 생성되지 않는다.

**결과:** (사용자 실기 테스트 후 기록)

---

### TC-6: Transcendence 종족 유닛들이 올바른 프리팹으로 생성된다
SINGLE-006

**전제:** 싱글플레이. Transcendence 종족을 선택한 상태.

**동작:**
1. Transcendence 종족으로 게임을 시작한다.
2. 배럭에서 유닛을 생산한다.

**기댓값:**
- Transcendence 종족 유닛 프리팹(BearGuard, FoxMagician, LionKnight)이 올바르게 생성된다.

**결과:** (사용자 실기 테스트 후 기록)

---

### TC-7: 멀티플레이에서 근접 유닛 공격이 동기화된다
MULTI-001

**전제:** Host(에디터) + Client(빌드) 구성. 양쪽 다른 종족 선택.

**동작:**
1. Host는 Spirit, Client는 Human으로 게임 시작.
2. Spirit 근접 유닛(FlameSpirit)이 적 유닛에 접근하여 공격한다.

**기댓값:**
- Host와 Client 양쪽 화면에서 FlameSpirit의 공격 애니메이션이 동시에 재생된다.
- 적 유닛의 HP가 양쪽에서 동일하게 감소한다.

**결과:** (사용자 실기 테스트 후 기록 — 에이전트 실기 불가, 사용자 확인 필요)

---

## QA 섹션 (qa-tester 전용)

### 정적 분석 대상 파일
- `Assets/_Project/Scripts/Domain/Unit/UnitType.cs`
- `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Editor/SetupUnitFactoryPrefabs.cs`

### 주요 확인 포인트
1. UnitType enum에 새 값 추가 시 switch/case 누락 여부 (UnitStats, UnitFactory, UnitProductionStats 등)
2. UnitFactory.GetPrefab()에서 null 반환 시 (미연결 슬롯) 안전하게 처리되는지
3. `FindFirstEnemyTargetByHexCoord`의 rangeThreshold가 range < 1.0일 때 1로 보정되는지
4. `RequestMove()` blocked 조건에서 아군 ClaimedTile은 여전히 포함되는지
5. 멀티플레이 NetworkCombatController에서 새 UnitType 값이 RPC로 직렬화되는지 (int 캐스팅)

---

## 정적 분석 결과 (qa-tester)

**분석일**: 2026-04-10
**분석 범위**: 변경 파일 6개 + UnitType 참조 파일 전수 검색 (Grep)

---

### 확인 포인트 1: UnitType switch/case 누락 여부

**검색 대상**: `UnitType.` 참조 파일 전체 (프로젝트 Scripts 디렉터리)

**결과 파일 목록**:
- `Domain/Unit/UnitStats.cs` — GetMaxHp / GetAttackPower / GetAttackRange / GetMoveSpeed / GetAttackCooldown / GetHitFrameTime 6개 메서드 모두 9종 유닛 case 포함. **누락 없음.**
- `Domain/Unit/UnitProductionStats.cs` — GetProductionTime / GetGoldCost 2개 메서드 모두 9종 유닛 case 포함. **누락 없음.**
- `Domain/Unit/UnitData.cs` — switch 없음. UnitStats.GetAttackCooldown / GetHitFrameTime을 생성자에서 직접 호출. **영향 없음.**
- `Infrastructure/Factories/UnitFactory.cs` — switch 없음. GetPrefab()은 종족(RaceId) 기준으로 리스트를 선택 후 UnitType으로 선형 탐색. **구조적으로 switch 불필요.**
- `Editor/SetupUnitFactoryPrefabs.cs` — switch 없음. `_raceMappings` 배열에 9종 유닛 전부 열거. **누락 없음.**
- `Presentation/UI/ProductionPanelUI.cs` — `GetPortrait()` 내부 switch가 Pistoleer/Assault/Sniper 3종만 처리, 나머지는 `_ => set.pistoleer` 폴백.

  **판정**: CONDITIONAL PASS (하단 이슈 1 참조)

---

### 확인 포인트 2: UnitFactory.GetPrefab() null 반환 시 안전 처리

**근거**: `UnitFactory.cs` 162~186번 라인
- `GetPrefab()` 반환값이 null이면 `Debug.LogError` 출력 후 `return` 처리됨.
- `Instantiate` 및 이후 초기화 코드가 실행되지 않음.
- NullReferenceException 발생 없음.

**판정**: PASS

---

### 확인 포인트 3: rangeThreshold 보정 (range < 1.0 근접 유닛)

**근거**: `UnitCombatUseCase.cs` 333번 라인
```
int rangeThreshold = Mathf.Max(1, Mathf.CeilToInt(attacker.AttackRange));
```
- FlameSpirit/EmberSpirit/BearGuard/LionKnight AttackRange = 0.5f
- CeilToInt(0.5f) = 1, Max(1, 1) = 1 → 인접 타일(distance=1)까지 탐색 가능
- Plan.md 명세와 정확히 일치.

`IsTargetInRange()` (432번 라인)에도 동일한 보정 로직 적용됨.

**판정**: PASS

---

### 확인 포인트 4: RequestMove() 아군 ClaimedTile 포함 여부

**근거**: `UnitMovementUseCase.cs` 59~68번 라인
```
if (other.Id != unit.Id && other.IsAlive && other.Team == unit.Team)
{
    blocked.Add(other.Position);
    if (other.ClaimedTile.HasValue)
        blocked.Add(other.ClaimedTile.Value);
}
```
- 아군(same Team) 유닛의 Position과 ClaimedTile 모두 blocked에 포함.
- 적 유닛은 조건문에서 제외 → 직진 경로 생성 가능.
- `IsTileBlockedBySameTeam()` (90~102번 라인)도 동일 조건으로 아군만 체크.

**판정**: PASS

---

### 확인 포인트 5: NetworkProductionController int ↔ UnitType 캐스팅

**근거**: `NetworkProductionController.cs` 내 RPC 메서드 전수 검색
- `RequestEnqueueServerRpc`: `int unitTypeInt` 수신 → `(UnitType)unitTypeInt` 변환 (264번 라인)
- `SpawnUnitClientRpc`: `int unitTypeInt` 수신 → `(UnitType)unitTypeInt` 변환 (392번 라인)
- `ProductionStartedClientRpc`: `(UnitType)unitTypeInt` 변환 (487, 493번 라인)
- `SyncQueueStateClientRpc`: `(UnitType)queue0TypeInt` 등 직접 캐스팅 (541, 542, 547~549, 557번 라인)
- `ToggleAutoServerRpc` / `AutoProductionChangedClientRpc`: 동일 패턴

모든 캐스팅은 `int → UnitType` 단순 캐스팅. enum 값이 0~8 연속 정수이므로 새 값(3~8) 추가 후에도 안전하게 변환됨. 단, 클라이언트가 서버보다 구버전인 경우 정의되지 않은 enum 값이 수신될 수 있으나, 이는 네트워크 버전 불일치 문제로 이번 작업 범위 외.

**NetworkCombatController**에는 UnitType 참조 없음 — 전투는 UnitData.Id 기반으로만 처리되므로 영향 없음.

**판정**: PASS

---

### 발견된 이슈

#### 이슈 1 (Minor): ProductionPanelUI.GetPortrait() — Spirit/Transcendence 유닛 초상화 폴백 처리

**위치**: `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` 814~820번 라인

**내용**:
`GetPortrait()` 메서드의 switch에 Pistoleer/Assault/Sniper 3종만 명시되어 있고, 나머지 6종(FlameSpirit 등)은 `_ => set.pistoleer` 폴백으로 처리됨.

현재 ProductionPanelUI는 Human 종족 전용 3버튼 구조(Pistoleer/Assault/Sniper)로만 구성되어 있고, Spirit/Transcendence 버튼은 미구현 상태임. 따라서 현 시점에서는 Spirit/Transcendence UnitType이 GetPortrait()에 전달되지 않으므로 **런타임 오동작 없음**.

단, 향후 Spirit/Transcendence 버튼 UI 구현 시 이 폴백이 잘못된 초상화(권총병 초상화)를 표시하는 버그로 이어질 수 있음. Plan.md 섹션 2에 "ProductionPanelUI.cs: UI 제작 후 별도 작업"으로 명시된 항목이므로 향후 작업 시 함께 처리 필요.

**심각도**: Minor (현재 실행 경로에서 미도달)
**권장 조치**: Spirit/Transcendence 버튼 UI 작업 시 함께 처리

---

#### 이슈 2 (Minor): UnitPortraitSet — Human 3종만 포함

**위치**: `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` 104~110번 라인

**내용**:
`UnitPortraitSet` 구조체에 `pistoleer`, `assault`, `sniper` 3개 필드만 존재. Spirit/Transcendence 초상화 필드가 없음.

GetPortrait() 미도달 이슈(이슈 1)와 동일하게, UI 작업 전까지는 실제 영향 없음. 향후 종족별 확장 시 구조체 자체를 종족별로 분리하거나 Dictionary 방식으로 변경하는 설계 고려 필요.

**심각도**: Minor
**권장 조치**: 이슈 1과 같이 Spirit/Transcendence UI 작업 시 처리

---

### TC 판정 (정적 분석 기반)

| TC ID | 제목 | 정적 분석 판정 | 비고 |
|-------|------|--------------|------|
| SINGLE-001 | 근접 유닛 공격 시작 | CONDITIONAL PASS | rangeThreshold=1 보정 확인됨. 실기 필요 |
| SINGLE-002 | 원거리 유닛 회귀 | CONDITIONAL PASS | blocked 조건 변경이 원거리 동작에 영향 없음 확인. 실기 필요 |
| SINGLE-003 | 근접 유닛 건물 공격 | CONDITIONAL PASS | HexCoord 폴백 건물 탐색도 동일 보정 적용. 실기 필요 |
| SINGLE-004 | 근접 유닛 직진 경로 | CONDITIONAL PASS | 적 Position이 blocked에서 제거됨 확인. 실기 필요 |
| SINGLE-005 | Spirit 유닛 프리팹 생성 | CONDITIONAL PASS | GetPrefab null 안전처리 확인. 에디터 스크립트 실행 후 Inspector 연결 여부 실기 확인 필요 |
| SINGLE-006 | Transcendence 유닛 프리팹 생성 | CONDITIONAL PASS | 동일 (SINGLE-005와 같음) |
| MULTI-001 | 멀티플레이 근접 유닛 동기화 | 에이전트 실기 불가 | 사용자 확인 필요. int↔UnitType 캐스팅 안전성 정적 확인됨 |
