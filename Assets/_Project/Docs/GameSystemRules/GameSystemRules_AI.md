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
- [6. Human 종족 참조 정보](#6-human-종족-참조-정보)
- [7. 아키텍처 및 구현 규칙](#7-아키텍처-및-구현-규칙)

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
| 골드 수입 배율 | goldIncomeMultiplier | 0.7 | 1.0 | 1.3 | AI 채굴소 수입에 곱하는 배율. 플레이어는 항상 1.0 |
| 생산 시간 배율 | productionTimeMultiplier | 1.3 | 1.0 | 0.7 | AI 유닛 생산 시간에 곱하는 배율. 클수록 생산이 느려짐 |
| 결정 주기 (초) | decisionInterval | 20 | 10 | 5 | 반응 시스템이 게임 상태를 점검하는 주기 |
| 유닛 수 비율 임계값 | unitCountThreshold | 0.7 | 1.0 | 1.3 | 규칙 R1 참조 — AI 유닛 수가 이 값 이하일 때 생산 활성화 |
| 골드 과잉 임계값 | goldExcessThreshold | 300 | 200 | 150 | 규칙 R2 참조 — AI 골드가 이 값 이상일 때 빌드오더 가속 |
| 빌드오더 가속 쿨다운 (초) | buildOrderAccelCooldown | 20 | 20 | 20 | 규칙 R2 쿨다운. 난이도 무관 동일값 |
| 생산 활성화 쿨다운 (초) | productionActivationCooldown | 15 | 15 | 15 | 규칙 R1 쿨다운. 난이도 무관 동일값 |
| 재시도 간격 (초) | retryInterval | 10 | 10 | 10 | 빌드오더 단계 실패 시 재시도 간격. 난이도 무관 동일값 |

---

## 3. 빌드오더 스크립트 시스템

### Phase 구조

**규칙 6. 4단계 Phase 분할**
빌드오더 스크립트는 4개의 Phase로 구분한다.
각 Phase는 이전 Phase가 완료된 후에 시작된다.

| Phase | 이름 | 목표 |
|-------|------|------|
| Phase 1 | 초반 | 첫 생산 건물 건설 + 기본 유닛 생산 시작 + 첫 채굴소 확보 |
| Phase 2 | 중반 | 첫 건물 업그레이드 + 두 번째 생산 라인 개설 |
| Phase 3 | 중후반 | 업그레이드 완성(3단계) + 생산 라인 확충 |
| Phase 4 | 후반 | 최대 생산 유지 + 공세 집중 |

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
| actionType | 행동 종류: PlaceBuilding / UpgradeBuilding / StartAutoProduction |
| buildingType | 대상 건물 타입 (PlaceBuilding / UpgradeBuilding 시 사용) |
| unitType | 자동생산 유닛 타입 (StartAutoProduction 시 사용) |
| targetBuildingLine | 대상 건물 라인 인덱스 (여러 생산 건물 중 몇 번째인지) |
| phaseIndex | 속하는 Phase 번호 (1~4) |
| delaySeconds | Phase 시작 후 이 항목까지의 기본 대기 시간 (쉬움/보통/어려움 각각 정의) |

**규칙 10. 종족별 별도 시나리오**
각 종족(Human / Spirit / Transcendence)은 독립적인 빌드오더 시나리오를 가진다.
Phase 구조(4단계)는 공통이지만, 건물 타입과 유닛 타입은 종족마다 다르다.
현재 Human 시나리오를 우선 설계하고, 나머지 종족은 동일한 Phase 구조를 재사용한다.

**규칙 11. 시나리오 저장 방식**
종족별 시나리오는 ScriptableObject(`AIScenarioConfig.asset`)에 직렬화하여 저장한다.
인스펙터에서 각 Phase별 항목 목록과 타이밍 값을 수정할 수 있어야 한다.

```
Assets/_Project/Resources/Config/
├── AIScenarioConfig_Human.asset
├── AIScenarioConfig_Spirit.asset      ← 추후 추가
└── AIScenarioConfig_Transcendence.asset  ← 추후 추가
```

---

## 4. 반응 시스템

### 결정 루프

**규칙 12. 폴링 기반 1회 행동**
반응 시스템은 `decisionInterval`마다 한 번씩 상태를 점검하고,
조건을 충족한 첫 번째 규칙 하나만 실행한 후 해당 사이클을 종료한다.
한 사이클에 여러 규칙이 동시에 실행되지 않는다.

**규칙 13. 규칙 우선순위**
규칙 목록을 위에서부터 순회하며 조건 충족 + 쿨다운 없음인 첫 번째 규칙만 실행한다.
아래 순서가 우선순위다:

1. 규칙 R1 (유닛 수 열세) → 더 시급한 전투 대응
2. 규칙 R2 (골드 과잉) → 자원 낭비 방지

### 규칙 R1 — 유닛 수 열세 시 자동생산 활성화

**규칙 14. 조건**
```
AI 현재 유닛 수 < 플레이어 현재 유닛 수 × unitCountThreshold
```
AND 현재 R1 쿨다운 없음

**규칙 15. 행동**
AI가 보유한 생산 건물 중 자동생산이 꺼진 건물이 있으면, 그 건물의 현재 단계에서 생산 가능한 유닛 중 가장 저렴한 유닛으로 자동생산을 활성화한다.
자동생산이 이미 모두 켜져 있으면 이 규칙은 행동을 수행하지 않는다.

**규칙 16. 쿨다운**
행동 수행 후 `productionActivationCooldown`(15초) 동안 R1은 재실행되지 않는다.
행동이 없어도(자동생산 모두 켜진 상태) 쿨다운은 적용되지 않는다.

---

### 규칙 R2 — 골드 과잉 시 빌드오더 가속

**규칙 17. 조건**
```
AI 현재 골드 ≥ goldExcessThreshold
```
AND 빌드오더에 대기 중인 다음 항목이 존재
AND 해당 항목의 `delaySeconds` 타이머가 아직 만료되지 않음
AND 현재 R2 쿨다운 없음

**규칙 18. 행동**
대기 중인 다음 빌드오더 항목의 타이머를 즉시 만료 처리하여 바로 실행 시도한다.

**규칙 19. 쿨다운**
가속 실행 후 `buildOrderAccelCooldown`(20초) 동안 R2는 재실행되지 않는다.

---

## 5. 가드 메커니즘

### 빌드오더 단계 실패 처리

**규칙 20. 재시도 방식**
빌드오더 단계가 실패(골드 부족, 타일 없음)하면 즉시 포기하거나 다음 단계로 넘어가지 않는다.
`retryInterval`(10초) 간격으로 같은 단계를 반복 시도한다.

**규칙 21. 골드 부족 시 생산 취소 1회 시도**
골드 부족이 원인인 경우, 수동 생산 큐에 있는 항목 중 IsCharged=false 항목 1개를 취소하여 골드를 확보 시도한다. 이 시도는 해당 빌드오더 단계에서 **딱 1회만** 허용된다.

| 시점 | 동작 |
|------|------|
| 골드 부족 첫 실패 | 수동 생산 취소 1회 시도 후 재시도 |
| 그 이후 실패 | 자연 누적 대기만 (취소 추가 불가) |

수동 생산(IsCharged=false)만 취소 대상이다. 이미 골드가 차감된 생산(IsCharged=true)은 취소하지 않는다.

**규칙 22. 자동생산 취소 금지**
자동생산 설정은 골드 확보 목적으로 취소하지 않는다.
자동생산은 규칙 R1에 의해서만 켜고 끈다.

**규칙 23. 타일 부족 시 대기**
타일 부족(배치 가능한 타일 없음)이 원인인 경우, 생산 취소 없이 `retryInterval` 대기 후 재시도한다.
타일은 전투 결과로 확보되므로 기다리면 해소된다.

### 행동 타입 쿨다운

**규칙 24. 쿨다운 독립 관리**
각 행동 타입(R1, R2)은 독립적인 쿨다운을 가진다.
R1 쿨다운 중이어도 R2는 실행 가능하다.

---

## 6. Human 종족 참조 정보

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

구체적인 Phase별 항목과 타이밍은 현재 문서 작성 후 별도 논의 예정.
결정 후 아래 섹션에 추가한다.

```
[Human 빌드오더 — 논의 예정]
Phase 1: ...
Phase 2: ...
Phase 3: ...
Phase 4: ...
```

---

## 7. 아키텍처 및 구현 규칙

### 레이어 귀속

**규칙 25. AIOpponentController 위치**
`Application/Services/AIOpponentController.cs`에 배치한다.
UseCase들(`BuildingPlacementUseCase`, `UnitProductionUseCase`, `ResourceUseCase`)을 생성자에서 주입받아 사용한다.
AI는 UseCase를 통해서만 게임 상태를 변경한다. Domain 레이어를 직접 조작하지 않는다.

**규칙 26. AIConfig / AIScenarioConfig 위치**
`Infrastructure/Config/` 아래에 ScriptableObject로 배치한다.
기존 `UnitStatsConfig`, `BuildingStatsConfig`와 동일한 패턴을 따른다.

### 초기화

**규칙 27. GameBootstrapper에서 초기화**
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

**규칙 28. AIOpponentController.Tick()**
`GameBootstrapper.Update()`에서 싱글플레이 전용 블록에 포함하여 매 프레임 Tick을 호출한다.
내부에서 빌드오더 타이머와 반응 시스템 폴링 타이머를 함께 갱신한다.

### 난이도 전달

**규칙 29. LocalPlayerDifficulty 정적 홀더**
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

**규칙 30. 씬 진입 시 AIConfig 선택**
`GameBootstrapper.InitializeAI()`에서 `LocalPlayerDifficulty.Current`를 읽어
해당 난이도의 `AIConfig` asset을 로드하고 `AIOpponentController`에 주입한다.
