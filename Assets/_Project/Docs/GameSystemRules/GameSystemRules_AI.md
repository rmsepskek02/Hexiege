# Game System Rules — AI 시스템

싱글플레이 전용 AI 상대(Red팀)의 동작 규칙 모음.
이 문서는 구현 기준 규칙을 정의하며, 종족별 구체적인 빌드오더 시나리오는 별도 논의 후 추가한다.

---

## 목차

- [1. 개요](#1-개요)
- [2. 난이도 파라미터 (AIConfig ScriptableObject)](#2-난이도-파라미터-aiconfig-scriptableobject)
- [3. 빌드오더 스크립트 시스템](#3-빌드오더-스크립트-시스템)
- [4. 반응 시스템](#4-반응-시스템)
- [5. 가드 메커니즘](#5-가드-메커니즘)
- [6. 건물 배치 로직](#6-건물-배치-로직)
- [7. Human 종족 참조 정보](#7-human-종족-참조-정보)
- [8. Spirit 종족 참조 정보](#8-spirit-종족-참조-정보)
- [9. Transcendence 종족 참조 정보](#9-transcendence-종족-참조-정보)
- [10. 아키텍처 및 구현 규칙](#10-아키텍처-및-구현-규칙)

---

## 1. 개요

**규칙 1. 적용 범위**
AI 시스템은 싱글플레이 전용이다. 멀티플레이에서는 절대 활성화하지 않는다.
`IsNetworkMode()` 가드로 GameBootstrapper에서 초기화를 차단한다.

**규칙 2. 제어 대상**
AI는 Red팀(TeamId.Red)만 제어한다. Blue팀(플레이어)에는 관여하지 않는다.

**규칙 3. 두 계층 구조**
AI 동작은 두 계층으로 구성된다.

| 계층 | 역할 | 특성 |
|------|------|------|
| 빌드오더 스크립트 | 어떤 건물을 짓고 언제 업그레이드할지 정의 | 사전 정의된 시간표, 순차 실행 |
| 반응 시스템 | 게임 상태를 주기적으로 감지하고 보조 행동 수행 | 폴링 기반, 조건 트리거 |

---

## 2. 난이도 파라미터 (AIConfig ScriptableObject)

### ScriptableObject 구조

`Assets/_Project/Resources/Config/AIConfig.asset` **하나의 파일**에 3개 난이도 파라미터를 모두 담는다.
기존 `UnitStatsConfig.asset`이 9개 유닛 스탯을 하나에 담는 것과 동일한 패턴이다.
인스펙터에서 쉬움/보통/어려움 3개 섹션의 수치를 한 곳에서 비교하며 수정할 수 있다.

```
Assets/_Project/Resources/Config/
└── AIConfig.asset   ← 단일 파일, 3 난이도 파라미터 포함
```

**규칙 4. 파라미터 구조 (DifficultyParams 중첩 구조체)**
AIConfig ScriptableObject는 `easy`, `normal`, `hard` 3개의 `DifficultyParams` 구조체를 필드로 가진다.
GameBootstrapper에서 `LocalPlayerDifficulty.Current`에 따라 해당 구조체를 AIOpponentController에 주입한다.

**규칙 5. 파라미터 목록**

| 파라미터 | 필드명 | 쉬움 | 보통 | 어려움 | 설명 |
|----------|--------|------|------|--------|------|
| 골드 수입 배율 | goldIncomeMultiplier | 0.7 | 1.0 | 1.3 | AI 채굴소 수입에 곱하는 배율. 플레이어는 항상 1.0. 적용 방식 → 규칙 34 참조 |
| 생산 시간 배율 | productionTimeMultiplier | 1.3 | 1.0 | 0.7 | AI 유닛 생산 시간에 곱하는 배율. 클수록 생산이 느려짐 |
| 결정 주기 (초) | decisionInterval | 20 | 10 | 5 | 반응 시스템이 게임 상태를 점검하는 주기 |
| 유닛 수 비율 임계값 | unitCountThreshold | 0.7 | 1.0 | 1.3 | 규칙 R1 참조 — AI 유닛 수가 이 값 이하일 때 생산 활성화 |
| 골드 과잉 임계값 | goldExcessThreshold | 300 | 200 | 150 | 규칙 R2 참조 — AI 골드가 이 값 이상일 때 빌드오더 가속 |
| 빌드오더 가속 쿨다운 (초) | buildOrderAccelCooldown | 20 | 20 | 20 | 규칙 R2 쿨다운. 난이도 무관 동일값 |
| 생산 활성화 쿨다운 (초) | productionActivationCooldown | 15 | 15 | 15 | 규칙 R1 쿨다운. 난이도 무관 동일값 |
| 재시도 간격 (초) | retryInterval | 10 | 10 | 10 | 빌드오더 단계 실패 시 재시도 간격. 난이도 무관 동일값 |
| 채굴소 탐지 주기 (초) | mineCheckInterval | 20 | 20 | 20 | MiningPost 존재 여부 탐지 및 재건 시도 주기. 난이도 무관 동일값 |

---

## 3. 빌드오더 스크립트 시스템

### Phase 구조

**규칙 6. 4단계 Phase 분할**
빌드오더 스크립트는 4개의 Phase로 구분한다.
각 Phase는 이전 Phase가 완료된 후에 시작된다.

| Phase | 이름 | 목표 |
|-------|------|------|
| Phase 1 | 초반 | 첫 생산 건물 건설 + 기본 유닛 생산 시작 |
| Phase 2 | 중반 | 첫 채굴소 확보 + 건물 업그레이드 또는 두 번째 생산 라인 개설 |
| Phase 3 | 중후반 | 업그레이드 완성(3단계) + 생산 라인 확충 |
| Phase 4 | 후반 | 두 번째 채굴소 확보 + 최대 생산 유지 + 공세 집중 |

**규칙 7. Phase 내 순서 보장**
동일 Phase 내의 빌드오더 항목은 정의된 순서대로 실행된다.
앞 항목이 완료되지 않으면 다음 항목은 실행되지 않는다.

**규칙 8. Phase 스킵 금지**
어떤 이유(골드 부족, 타일 없음 등)로도 Phase를 건너뛰지 않는다.
현재 단계의 항목이 완료될 때까지 재시도한다. (→ 가드 메커니즘 섹션 참조)
이 게임은 순차 테크트리 기반이므로 단계 건너뜀은 AI 동작을 깨뜨린다.

### 빌드오더 항목 구조

**규칙 9. 항목 데이터**
빌드오더 한 항목(Step)은 다음 정보를 포함한다.

| 필드 | 설명 |
|------|------|
| actionType | 행동 종류: PlaceBuilding / UpgradeBuilding / StartProduction |
| buildingType | 대상 건물 타입 (PlaceBuilding / UpgradeBuilding 시 사용) |
| unitType | 생산 유닛 타입 (StartProduction 시 사용) |
| targetBuildingLine | 대상 건물 라인 인덱스 (여러 생산 건물 중 몇 번째인지) |
| phaseIndex | 속하는 Phase 번호 (1~4) |
| delaySeconds | Phase 시작 후 이 항목까지의 기본 대기 시간 (쉬움/보통/어려움 각각 정의) |

**규칙 10. 종족별 별도 시나리오**
각 종족(Human / Spirit / Transcendence)은 독립적인 빌드오더 시나리오를 가진다.
Phase 구조(4단계)는 공통이지만, 건물 타입과 유닛 타입은 종족마다 다르다.
3종족 모두 시나리오 문서가 작성 완료되었다.

**규칙 11. 시나리오 저장 방식**
종족별 시나리오는 ScriptableObject(`AIScenarioConfig.asset`)에 직렬화하여 저장한다.
인스펙터에서 각 Phase별 항목 목록과 타이밍 값을 수정할 수 있어야 한다.

```
Assets/_Project/Resources/Config/
├── AIScenarioConfig_Human.asset
├── AIScenarioConfig_Spirit.asset
└── AIScenarioConfig_Transcendence.asset
```

### StartProduction 실행 방식

**규칙 12. StartProduction 콜백 기반 생산**
`StartProduction` 빌드오더 단계가 실행되면 다음 두 가지가 일어난다.

1. `_lineProduction[lineIndex] = unitType` 기록 — 이 라인의 배럭이 앞으로 생산할 유닛 타입을 저장
2. 해당 배럭에 `EnqueueUnit(barracksId, unitType)` 1회 호출 — 초기 큐 시드

이후 `GameEvents.OnUnitProduced` 이벤트를 구독하여 Red 팀 유닛이 생산될 때마다 콜백을 받는다.
콜백에서 `event.BarracksId`로 `_lineProduction`을 조회해 `EnqueueUnit`을 즉시 호출한다.
유닛이 나오는 순간 다음 유닛이 등록되므로 타이머 없이 생산이 끊기지 않고 유지된다.

골드가 부족해 `EnqueueUnit`이 실패해도 다음 유닛 생산 완료 시 다시 시도하므로 별도 재시도 로직이 불필요하다.

> **구현 참고**: `UnitProducedEvent`에 `BarracksId` 필드 추가 필요.
> `UnitProductionUseCase.CompleteProduction`에서 이벤트 발행 시 `state.BarracksId` 전달.
> 기존 구독자(`ProductionTicker`)는 이 필드를 사용하지 않으므로 하위 호환성 문제 없음.

---

## 4. 반응 시스템

### 결정 루프

**규칙 13. 폴링 기반 1회 행동**
반응 시스템은 `decisionInterval`마다 한 번씩 상태를 점검하고,
조건을 충족한 첫 번째 규칙 하나만 실행한 후 해당 사이클을 종료한다.
한 사이클에 여러 규칙이 동시에 실행되지 않는다.

**규칙 14. 규칙 우선순위**
규칙 목록을 위에서부터 순회하며 조건 충족 + 쿨다운 없음인 첫 번째 규칙만 실행한다.
아래 순서가 우선순위다:

1. 규칙 R1 (유닛 수 열세) → 더 시급한 전투 대응
2. 규칙 R2 (골드 과잉) → 자원 낭비 방지
3. 규칙 R3 (MiningPost 파괴 감지) → 중립 자원 거점 복구

### 규칙 R1 — 유닛 생산 긴급 보충

**규칙 15. 조건**
```
AI 현재 유닛 수 < 플레이어 현재 유닛 수 × unitCountThreshold
```
AND 현재 R1 쿨다운 없음

**규칙 16. 행동**
`_lineProduction`에 배럭-유닛 타입이 등록된 모든 Red 생산 건물에 대해 즉시 `EnqueueUnit(barracksId, _lineProduction[lineIndex])`를 호출한다.
평시에는 `OnUnitProduced` 콜백이 지속 생산을 유지하지만, 유닛이 전투에서 급감한 경우 이 긴급 보충이 큐를 즉시 채운다.
큐가 가득 차거나 골드가 부족한 경우 `EnqueueUnit`이 자동으로 실패를 반환하므로 별도 상태 확인이 불필요하다.

**규칙 17. 쿨다운**
행동 수행 후 `productionActivationCooldown`(15초) 동안 R1은 재실행되지 않는다.
콜백 기반 정상 생산 중에는 이 규칙이 실행되지 않으므로 쿨다운도 발생하지 않는다.

---

### 규칙 R2 — 골드 과잉 시 빌드오더 가속

**규칙 18. 조건**
```
AI 현재 골드 ≥ goldExcessThreshold
```
AND 빌드오더에 대기 중인 다음 항목이 존재
AND 해당 항목의 `delaySeconds` 타이머가 아직 만료되지 않음
AND 현재 R2 쿨다운 없음

**규칙 19. 행동**
대기 중인 다음 빌드오더 항목의 타이머를 즉시 만료 처리하여 바로 실행 시도한다.

**규칙 20. 쿨다운**
가속 실행 후 `buildOrderAccelCooldown`(20초) 동안 R2는 재실행되지 않는다.

---

## 5. 가드 메커니즘

### 빌드오더 단계 실패 처리

**규칙 21. 재시도 방식**
빌드오더 단계가 실패(골드 부족, 타일 없음)하면 즉시 포기하거나 다음 단계로 넘어가지 않는다.
`retryInterval`(10초) 간격으로 같은 단계를 반복 시도한다.

**규칙 22. 골드 부족 시 생산 취소 1회 시도**
골드 부족이 원인인 경우, 수동 생산 큐에 있는 항목 중 IsCharged=false 항목 1개를 취소하여 골드를 확보 시도한다. 이 시도는 해당 빌드오더 단계에서 **딱 1회만** 허용된다.

| 시점 | 동작 |
|------|------|
| 골드 부족 첫 실패 | 수동 생산 취소 1회 시도 후 재시도 |
| 그 이후 실패 | 자연 누적 대기만 (취소 추가 불가) |

수동 생산(IsCharged=false)만 취소 대상이다. 이미 골드가 차감된 생산(IsCharged=true)은 취소하지 않는다.

**규칙 23. 자동생산 미사용**
AI는 자동생산 기능을 사용하지 않는다.
유닛 생산은 `StartProduction` 단계의 초기 시드 `EnqueueUnit`과 `OnUnitProduced` 콜백으로만 유지되며,
유닛 수 열세 시 R1 긴급 보충이 담당한다.
골드 확보를 위해 자동생산을 켜거나, 이미 등록된 콜백을 해제하는 행동을 하지 않는다.

**규칙 24. 타일 부족 시 대기**
타일 부족(배치 가능한 타일 없음)이 원인인 경우, 생산 취소 없이 `retryInterval` 대기 후 재시도한다.
타일은 전투 결과로 확보되므로 기다리면 해소된다.

### 행동 타입 쿨다운

**규칙 25. 쿨다운 독립 관리**
각 행동 타입(R1, R2)은 독립적인 쿨다운을 가진다.
R1 쿨다운 중이어도 R2는 실행 가능하다.

---

## 6. 건물 배치 로직

### 일반 건물 배치

**규칙 26. BFS 기반 배치 타일 선택**
AI가 건물을 배치할 타일은 다음 방식으로 결정한다.

1. Red 성채를 시작점으로 BFS 탐색
2. **일반 건설 판정 조건**을 만족하는 타일 중 가장 가까운 타일을 후보로 선택한다. 그 조건의 소유권 항목은 AI 팀인 Red 소유를 뜻한다. **판정 조건의 단일 소스는 `TechnicalDesignDocument.md` 「`HexTile` 런타임 상태 계약」 절의 판정 규칙이므로 여기에 옮겨 적지 않는다.**
3. **후보 타일이 기존 Red 생산 건물의 인접 6타일에 해당하면 건너뜀**
4. 조건을 만족하는 첫 번째 타일에 배치

**후보가 거부되면 탐색을 멈추지 않는다.** 2단계에서 조건을 만족하지 못했거나 3단계에서 건너뛴 타일이 나와도 그 자리에서 끝내지 않고 BFS 순서의 다음 후보로 계속 진행한다. 탐색을 끝내는 경우는 조건을 만족하는 타일에 배치했을 때와, BFS가 닿는 타일을 전부 확인했는데 조건을 만족하는 타일이 하나도 없을 때뿐이다. 후자는 타일 부족이므로 **규칙 24**(타일 부족 시 대기)를 따른다.

배럭 인접 타일을 보존하는 이유: `UnitProductionUseCase.FindSpawnTile`이 배럭 인접 6타일 중 walkable + 유닛 없는 타일에 유닛을 스폰한다. 모든 인접 타일이 건물로 막히면 생산 완료 후에도 유닛이 영구적으로 나오지 못하는 블로킹 상태가 된다.

> ⏳ **무작위 맵 구현 시 배치 후보 판정을 일반 건설 조건으로 전환한다.** 현재 코드는 후보를 **이동 가능 여부**로 고르고 있으며, **전환 전까지는 그것이 맞다** — 지금 코드에는 일반 건설 판정이 읽는 타일 상태 축 자체가 없어 다른 조건을 쓸 수 없다.
> 무작위 맵에는 **이동·점령은 되지만 일반 건설만 막히는 타일**이 생기고(단일 소스는 `GameSystemRules_RandomMap.md` 규칙 9 「건설 불가 구역」), 그 구역은 맵 유형이 지정한 곳에만 두므로 **맵 유형 5종 중 3종에서만** 나타난다(같은 문서 3장 ⑤). 그때부터 이동 기준과 건설 기준이 갈라지므로 위 2단계의 조건이 필요해진다.
> 전환 시점과 전환 내용의 단일 소스는 `TechnicalDesignDocument.md` 「기존 코드 전환 요구」 절이다.
> **그때가 오기 전에 코드 표기를 미리 바꾸지 않는다** — 지금 바꾸면 현재 코드와 어긋난다.

**사용 API (모두 public 확인 완료):**

| 목적 | API |
|------|-----|
| 건물 배치 유효성 확인 | `BuildingPlacementUseCase.CanPlaceBuildingType(type, position, team)` |
| 건물 배치 실행 | `BuildingPlacementUseCase.PlaceBuilding(type, team, position, race)` |
| 전체 건물 조회 | `BuildingPlacementUseCase.Buildings` (IReadOnlyDictionary) |
| 생산 건물 여부 확인 | `BuildingTypeHelper.IsProductionBuilding(type)` |

---

### 채굴소(MiningPost) 배치 — 병행 트랙

MiningPost는 Build Order 항목에서 제거하고, Phase 진입 시 독립적으로 동작하는 병행 트랙이 처리한다.

**규칙 27. MiningPost 병행 트랙**
Phase 2 및 Phase 4 진입 시 각각 병행 트랙이 자동 시작된다.

트랙 실행 흐름:
1. `isTrackActive = true`
2. `BuildingPlacementUseCase.Buildings`에서 Red 생산 건물 전체 조회
3. 모든 생산 건물에 `SetRallyPoint` → 대상 금광 인접 타일 지정
4. 매 `mineCheckInterval`마다 `PlaceMiningPost` 조건 확인
   - 미충족: 유닛들이 랠리포인트 방향으로 행군하며 인접 타일을 자연스럽게 점령
   - Blue 점령 상태: 행군 중인 유닛이 자동 전투로 Blue MiningPost 파괴 후 배치
5. 배치 성공 → 모든 생산 건물 `ClearRallyPoint` → `isTrackActive = false`

> Phase 2: 첫 번째 중립 금광 타겟
> Phase 4: 두 번째 중립 금광 타겟 (Phase 2에서 점령하지 않은 쪽)

**광산 타일 위치 조회 방식**
AI는 맵마다 광산 타일 위치가 다를 수 있으므로 고정 좌표를 사용하지 않는다.
`HexGrid.Tiles`(IReadOnlyDictionary)를 순회하여 `HasGoldMine == true`인 타일을 필터링하고,
이미 Red MiningPost가 없는 타일을 Phase별 타겟 순서대로 선택한다.

> ⏳ **무작위 맵 구현 시 `MineKind` 기반 조회로 전환된다.** 위 서술과 아래 표의 광산 타일 조회는 **현재 코드를 그대로 적은 것이며 지금은 유효하다.**
> 무작위 맵을 구현하는 시점에 광산 타일의 정체성 표현이 `MineKind` 로 바뀌고, 이 조회도 그 기준으로 옮겨진다.
> 전환 시점과 전환 내용의 단일 소스는 `TechnicalDesignDocument.md` 「기존 코드 전환 요구」 절이다(그 절이 속한 「무작위 맵 시작 동기화」는 확정 설계·미구현 상태다).
> **그때가 오기 전에 이 문서의 표기를 미리 바꾸지 않는다** — 지금 바꾸면 현재 코드와 어긋난다.

**사용 API:**

| 목적 | API |
|------|-----|
| 채굴소 배치 | `BuildingPlacementUseCase.PlaceMiningPost(team, position, race)` |
| 랠리포인트 설정 | `UnitProductionUseCase.SetRallyPoint(barracksId, target)` |
| 랠리포인트 초기화 | `UnitProductionUseCase.ClearRallyPoint(barracksId)` |
| 광산 타일 조회 | `HexGrid.Tiles.Values.Where(t => t.HasGoldMine)` |

---

### 규칙 R3 — MiningPost 파괴 감지 및 재건

**탐지 주기:** `mineCheckInterval`(기본 20초, AIConfig에서 설정)

**조건:**
```
점령 완료되었어야 하는 금광에 Red MiningPost 없음
AND isTrackActive == false
```

**행동:** 병행 트랙 재시작 (`isTrackActive = true` → SetRallyPoint → …)

`isTrackActive` 플래그가 중복 실행을 방지하고, `mineCheckInterval`이 탐지 주기와 자연 쿨다운을 동시에 담당한다. 트랙 실행 중에는 `isTrackActive == true`이므로 Reaction System이 중복으로 트랙을 시작하지 않는다.

---

## 7. Human 종족 참조 정보

### 생산 건물 라인 (BuildingType.cs 기준)

| 라인 | Stage 1 | Stage 2 | Stage 3 |
|------|---------|---------|---------|
| 근거리A | TrainingCamp (7) | WarAcademy (8) | HumanBarracks (9) |
| 총기류 | Gunsmith (10) | Armory (11) | WeaponForge (12) |
| 탈것류 | Garage (13) | VehicleBay (14) | — (2단계까지) |

### 건물-유닛 매핑 (UnitType.cs 기준)

각 라인에서 어느 단계에 어떤 유닛이 해금되는지를 정의한다.

| 라인 | requiredStage=1 | requiredStage=2 | requiredStage=3 |
|------|----------------|----------------|----------------|
| 근거리A | LittleKnight (3) | SpearMan (4) | BattleAxe (5) |
| 총기류 | Pistoleer (0) | Assault (1) | Sniper (2) |
| 탈것류 | CannonCart (7) | Tank (6) | — |

> ⚠️ 위 매핑은 `BuildingType.cs` 라인 주석과 `UnitType.cs` 설명에서 추론한 값이다.
> 실제 Inspector의 `ProductionPanelUI._buildingUnitMappings` 설정과 반드시 일치시킨다.

### AI 빌드오더 시나리오

→ **[GameSystemRules_AI_Scenario_Human.md](GameSystemRules_AI_Scenario_Human.md)** 참조

---

## 8. Spirit 종족 참조 정보

### 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 |
|------|---------|---------|---------|
| 0 — 불 | FireSpire | BlazeConduit | InfernoCore |
| 1 — 물 | AquaSpring | TidalNexus | OceanicHeart |
| 2 — 땅 | StoneMound | TerraForge | GaeaSanctum |

### 건물-유닛 매핑

| 라인 | requiredStage=1 | requiredStage=2 | requiredStage=3 |
|------|----------------|----------------|----------------|
| 0 — 불 | EmberSpirit | FlameSpirit | InfernoSpirit |
| 1 — 물 | TideSpirit | StreamSpirit | TorrentSpirit |
| 2 — 땅 | DustSpirit | BoulderSpirit | QuakeSpirit |

### AI 빌드오더 시나리오

→ **[GameSystemRules_AI_Scenario_Spirit.md](GameSystemRules_AI_Scenario_Spirit.md)** 참조

---

## 9. Transcendence 종족 참조 정보

### 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 |
|------|---------|---------|---------|
| 0 — 동물A | PrimalAltar | PrimalDen | PrimalSanctuary |
| 1 — 동물B | FeralAltar | FeralDen | FeralSanctuary |
| 2 — 식물 | SporePatch | FloralNursery | — (2단계까지) |

### 건물-유닛 매핑

| 라인 | requiredStage=1 | requiredStage=2 | requiredStage=3 |
|------|----------------|----------------|----------------|
| 0 — 동물A | RabbitTrickster | RhinoBreaker | BearGuard |
| 1 — 동물B | FoxMagician | EagleArcher | LionKnight |
| 2 — 식물 | MushroomBomber | BloomFairy | — |

> 식물 라인은 2단계(FloralNursery)가 최상위다.

### AI 빌드오더 시나리오

→ **[GameSystemRules_AI_Scenario_Transcendence.md](GameSystemRules_AI_Scenario_Transcendence.md)** 참조

---

## 10. 아키텍처 및 구현 규칙

### 레이어 귀속

**규칙 28. AIOpponentController 위치**
`Application/Services/AIOpponentController.cs`에 배치한다.
UseCase들(`BuildingPlacementUseCase`, `UnitProductionUseCase`, `ResourceUseCase`)을 생성자에서 주입받아 사용한다.
AI는 UseCase를 통해서만 게임 상태를 변경한다. Domain 레이어를 직접 조작하지 않는다.

**규칙 29. AIConfig / AIScenarioConfig 위치**
`Infrastructure/Config/` 아래에 ScriptableObject로 배치한다.
기존 `UnitStatsConfig`, `BuildingStatsConfig`와 동일한 패턴을 따른다.

### 초기화

**규칙 30. GameBootstrapper에서 초기화**
`GameBootstrapper.Setup.cs`에 `InitializeAI()` 헬퍼를 추가하여 `LoadMap()` 호출 직후 실행한다.
`IsNetworkMode()` 가드로 싱글플레이에서만 초기화한다.

```
싱글플레이 시작 순서:
  GameRaceContext.Set(...)
  LoadMap(...)
    CreateUseCases()
    InitializeAI()   ← 여기서 AIOpponentController 생성 및 UseCase 주입
  ...
```

**규칙 30-A. AI On/Off 토글**
AI 활성화 여부는 `AIConfig` ScriptableObject의 `enableAI` 필드로 제어한다.
`Resources/Config/AIConfig.asset`을 Project 창에서 선택하면 씬을 열지 않고 Inspector에서 바로 토글할 수 있다.

```csharp
// AIConfig.cs
[Header("AI On/Off")]
[Tooltip("false로 끄면 싱글플레이에서도 AI가 동작하지 않는다.")]
public bool enableAI = true;

// GameBootstrapper.Setup.cs — InitializeAI() 내부
var aiConfig = Resources.Load<AIConfig>("Config/AIConfig");
if (aiConfig == null || !aiConfig.enableAI)
{
    Debug.Log("[GameBootstrapper] AI 비활성화 상태.");
    return;
}

// GameBootstrapper.Map.cs — LoadMap() 내부
if (!NetworkContext.IsNetworkActive)
    InitializeAI();   // enableAI 체크는 InitializeAI() 내부에서 수행
```

`GameBootstrapper` 씬 레벨 SerializeField가 아닌 `AIConfig` 에셋으로 두는 이유:
Game.unity 씬을 열지 않아도 Project 창에서 에셋을 선택해 즉시 토글할 수 있어 테스트 편의성이 높다.

**규칙 31. AIOpponentController.Tick()**
`GameBootstrapper.Update()`에서 싱글플레이 전용 블록에 포함하여 매 프레임 Tick을 호출한다.
내부에서 빌드오더 타이머와 반응 시스템 폴링 타이머를 함께 갱신한다.

### 난이도 전달

**규칙 32. LocalPlayerDifficulty 정적 홀더**
로비에서 선택한 난이도를 게임 씬으로 전달하는 정적 홀더 클래스 `LocalPlayerDifficulty`를 `Infrastructure` 레이어에 추가한다.
기존 `LocalPlayerRace` 패턴과 동일하게 구현한다.

```
// 난이도 열거형
public enum DifficultyLevel { Easy, Normal, Hard }

// 정적 홀더 (Infrastructure)
public static class LocalPlayerDifficulty
{
    public static DifficultyLevel Current { get; set; } = DifficultyLevel.Normal;
}
```

**규칙 33. 씬 진입 시 AIConfig 선택**
`GameBootstrapper.InitializeAI()`에서 `LocalPlayerDifficulty.Current`를 읽어
해당 난이도의 `AIConfig` asset을 로드하고 `AIOpponentController`에 주입한다.

### 로비 난이도 선택 UI

**규칙 35. 난이도 선택 화면 흐름**
싱글플레이 난이도는 로비의 별도 화면에서 선택한다. 흐름은 다음과 같다.

```
BattleMainView
  → [싱글플레이] 버튼 클릭
  → BattleScreen.SingleplayDifficulty 상태로 전환
  → DifficultySelectView 표시 (쉬움 / 보통 / 어려움 / 뒤로가기)
  → 난이도 버튼 클릭
  → LocalPlayerDifficulty.Current = 선택값
  → 게임 씬 로드
```

**규칙 36. 구현 변경 범위**
기존 `BattleScreen` 열거형 패턴을 그대로 재사용하며 상태 하나만 추가한다.

| 변경 대상 | 내용 |
|----------|------|
| `BattleViewModel.BattleScreen` enum | `SingleplayDifficulty` 상태 추가 |
| `BattleViewModel` | `CmdStartSingleplay` → `CurrentScreen = SingleplayDifficulty`로 변경. `CmdSelectDifficulty(DifficultyLevel)` 커맨드 추가 (LocalPlayerDifficulty 설정 + 씬 로드) |
| `DifficultySelectView` | 신규 View — 쉬움/보통/어려움 버튼 3개 + 뒤로가기 버튼. `BattleViewModel`에 바인딩 |
| `BattleRootView` | `DifficultySelectView` 참조 추가 및 `Bind()`에서 바인딩 |

뒤로가기 버튼은 `CurrentScreen = BattleScreen.Main`으로 복귀한다.
난이도를 선택하지 않고 뒤로 나가면 `LocalPlayerDifficulty.Current`는 변경되지 않는다(이전 값 또는 기본값 `Normal` 유지).

---

### 골드 수입 배율 적용

**규칙 34. goldIncomeMultiplier 적용 방식 (Option C)**
`goldIncomeMultiplier`는 게임 초기화 시 한 번만 적용하는 방식을 따른다.

- `GameBootstrapper.InitializeAI()`에서 `ResourceUseCase.SetIncomeMultiplier(TeamId.Red, config.goldIncomeMultiplier)` 를 1회 호출
- `ResourceUseCase` 내부에서 배율 값을 저장하고, `TickIncome` 실행 시 Red 팀 수입에 배율을 곱해 적용
- Blue 팀(플레이어)은 항상 1.0 배율이며, AI의 배율만 이 API로 조정

이 방식은 `TickIncome` 호출자(`GameBootstrapper`) 를 수정하지 않아도 되며,
`ResourceUseCase` 외부에서 배율 계산 로직을 갖지 않아 의존성이 최소화된다.

> **구현 참고**: `ResourceUseCase`에 `SetIncomeMultiplier(TeamId team, float multiplier)` 메서드 추가 필요.
> 내부 필드 `_incomeMultipliers[team]`에 저장 후 `TickIncome`에서 적용.
