# Research — 방어 타워 공격 기능 구현

## 이 작업은 무엇인가?

Hexiege 게임에는 각 종족마다 하나씩 배치할 수 있는 방어 타워가 있습니다.
인간은 "CannonTower", 정령은 "RuneSpire", 초월은 "VineTower"라는 이름을 가집니다.
이 타워들은 기획(StatsReference.md)과 코드(BuildingType.AutoTower)가 이미 정의되어 있지만,
실제로 적 유닛을 공격하는 동작은 아직 구현되지 않은 상태입니다.

이 작업은 타워가 사거리 내에 들어온 적 유닛을 자동으로 감지하고,
쿨다운마다 데미지를 주는 기능을 구현하는 것입니다.

---

## 현재 코드 상태

### 1. 이미 준비된 것

| 항목 | 파일 | 상태 |
|------|------|------|
| BuildingType.AutoTower (값=2) | Domain/Building/BuildingType.cs | ✅ 정의됨 |
| BuildingStats.StatValues.AttackPower | Domain/Building/BuildingStats.cs | ✅ 필드 있음 |
| BuildingStats.StatValues.AttackCooldown | Domain/Building/BuildingStats.cs | ✅ 필드 있음 |
| BuildingStatsConfig.humanAttackPower/Cooldown | Infrastructure/Config/BuildingStatsConfig.cs | ✅ Inspector 필드 있음 |
| IDamageable 인터페이스 | Domain/IDamageable.cs | ✅ 유닛·건물 공통 |

### 2. 누락된 것

| 항목 | 현재 상태 | 필요 이유 |
|------|----------|-----------|
| AttackRange (공격 사거리) | StatValues/BuildingStatsConfig에 없음 | 타워가 4.0 타일 사거리로 탐색해야 함 |
| BuildingData.AttackCooldownRemaining | BuildingData에 없음 | 타워별 쿨다운 타이머 추적 필요 |
| 타워 공격 UseCase | 없음 | 주기적 타겟 탐색 + 데미지 적용 로직 |
| 멀티플레이 타워 공격 처리 | NetworkCombatController에 없음 | 서버 권위 처리 필요 |
| BuildingStatsConfig.asset에 AutoTower 스탯 | Inspector에 미입력 상태 추정 | 실제 값이 없으면 모두 0 반환 |

---

## 기획 스탯 (StatsReference.md 기준)

| 종족 | 건물명 | HP | 건설비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|--------|-----|---------|--------|------------|------------|
| Human | CannonTower | 50 | 150 | 15 | 4.0 타일 | 5.0s |
| Spirit | RuneSpire | 150 | 200 | 15 | 4.0 타일 | 3.5s |
| Transcendence | VineTower | 100 | 175 | 15 | 4.0 타일 | 5.0s |

세 종족 모두 동일한 `BuildingType.AutoTower`를 사용하고, 수치만 다릅니다.

---

## 기존 유닛 전투 구조와의 비교

유닛 전투 (`UnitCombatUseCase` + `NetworkCombatController`)와 타워 전투의 핵심 차이:

| 항목 | 유닛 | 방어 타워 |
|------|------|---------|
| 이동 | A* 이동 → 추격 → 공격 | 이동 없음 (고정) |
| 상태 머신 | 3단계 (이동/추격/공격) | 단순 (감지/공격) |
| 타겟 종류 | 적 유닛 + 적 건물 | 적 유닛만 |
| 쿨다운 관리 | UnitData.AttackCooldownRemaining | BuildingData에 추가 필요 |
| 싱글플레이 | UnitView 코루틴에서 TryAttack 직접 호출 | 별도 타워 UseCase Tick |
| 멀티플레이 | NetworkCombatController가 서버에서 일괄 처리 | 동일 컨트롤러에 타워 처리 추가 |

---

## 영향 범위 분석

### 수정이 필요한 파일

| 파일 | 수정 내용 | 레이어 |
|------|----------|--------|
| BuildingStats.cs (StatValues) | AttackRange 필드 추가 | Domain |
| BuildingData.cs | AttackCooldownRemaining 런타임 필드 추가 | Domain |
| BuildingStatsConfig.cs (BuildingTypeEntry) | attackRange 필드 3종족분 추가 | Infrastructure |
| GameBootstrapper.cs | InitializeBuildingStatsFromConfig에 AttackRange 매핑 추가, TowerCombatUseCase 생성·연결 | Bootstrap |

### 신규 파일

| 파일 | 역할 | 레이어 |
|------|------|--------|
| TowerCombatUseCase.cs | 타워의 주기적 타겟 탐색 + 데미지 처리 | Application |

### 수정이 필요한 기존 파일 (멀티플레이)

| 파일 | 수정 내용 |
|------|----------|
| NetworkCombatController.cs | 서버 Update에서 타워 공격 Tick 추가 |

### Inspector 작업

| 에셋 | 작업 |
|------|------|
| BuildingStatsConfig.asset | AutoTower의 종족별 HP/비용/공격력/쿨다운/사거리 값 입력 |

---

## 의존 관계

TowerCombatUseCase가 필요로 하는 의존성:
- `UnitSpawnUseCase` — 모든 유닛 목록 조회 (적 탐색)
- `BuildingPlacementUseCase` — 타워 위치 및 목록 조회
- `IHexCoordinateMapper` — 월드 좌표 변환 (거리 계산)
- `IEntityPositionProvider` — 유닛/타워 실제 Transform 위치 (Lerp 중 정확도)

이 의존성은 모두 `UnitCombatUseCase`가 이미 사용하고 있는 것들입니다.

---

## 부가 확인 사항

- `BuildingStatsConfig.asset`(Inspector)에 AutoTower 항목이 현재 입력되어 있는지 Unity에서 직접 확인 필요
- NetworkCombatController는 유닛 전투만 처리하며 타워 쿨다운 감소 코드가 없음 → 타워 쿨다운은 별도로 처리해야 함
