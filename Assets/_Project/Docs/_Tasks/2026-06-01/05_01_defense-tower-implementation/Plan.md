# Plan — 방어 타워 공격 기능 구현

## 이 계획은 무엇인가?

방어 타워(CannonTower/RuneSpire/VineTower)가 사거리 내에 들어온 적 유닛을 자동으로 공격하는 기능을 구현합니다.
타워는 이동하지 않으므로 유닛 전투(3단계 상태 머신)보다 훨씬 단순하며,
기존 `UnitCombatUseCase`의 타겟 탐색·데미지·사망 처리 구조를 최대한 재사용합니다.

싱글플레이와 멀티플레이(서버 권위) 모두 지원합니다.

---

## GameSystemRules.md 규칙 근거

방어 타워 시스템 규칙은 `GameSystemRules.md — ## 방어 타워 시스템` 섹션에 정의됨.

| 규칙 | 내용 | 구현 적용 |
|------|------|-----------|
| 규칙 1 (타겟 대상) | 적 유닛만 타겟, 건물/아군 제외 | TowerCombatUseCase 탐색 시 적 유닛만 순회 |
| 규칙 2 (타겟 선택 기준) | 가장 가까운 적 유닛 | Vector3.Distance 최솟값 기준 선택 |
| 규칙 3 (사거리 판정) | 월드 좌표 Vector3.Distance 기준 | 기존 UnitCombatUseCase와 동일한 방식 |
| 규칙 4 (배치 직후 첫 공격) | 배치 즉시 공격 (쿨다운 0) | BuildingData.AttackCooldownRemaining 초기값 0 |
| 규칙 5 (공격 후 쿨다운) | 공격 직후 쿨다운 시작 | 공격 후 AttackCooldown 값으로 리셋 |
| 규칙 6 (타겟 사망 시) | 즉시 새 타겟 탐색, 기존 쿨다운 유지 | 사망 이벤트 후 쿨다운 리셋 없이 다음 Tick에서 탐색 |
| 규칙 7 (타워 파괴 시) | 모든 동작 즉시 중단 | IsAlive 체크로 Tick 내부에서 자동 처리 |
| 규칙 8 (배치 제한) | 무제한 | 별도 제한 로직 불필요 |
| 규칙 9 (멀티플레이) | 서버 권위 처리 | NetworkCombatController 서버 Tick에서만 호출 |
| 규칙 10 (UI 패널) | 기존 건물 팝업 UI 그대로 사용 | 별도 타워 전용 UI 제작 없음 |

---

## 구현 순서 및 수정 내용

### Step 1 — Domain 데이터 구조 확장

**[1-1] `BuildingStats.StatValues`에 AttackRange 추가**
- 파일: `Assets/_Project/Scripts/Domain/Building/BuildingStats.cs`
- StatValues 구조체에 `public float AttackRange;` 필드 추가
- `GetAttackRange(BuildingType, RaceId)` 조회 메서드 추가 (GetAttackPower와 동일한 패턴)

**[1-2] `BuildingData`에 쿨다운 런타임 상태 추가**
- 파일: `Assets/_Project/Scripts/Domain/Building/BuildingData.cs`
- `public float AttackCooldownRemaining { get; set; }` 추가
- BuildingData 생성자에서 초기값 0으로 설정
- 근거: UnitData.AttackCooldownRemaining과 동일한 패턴, 타워별 쿨다운 타이머 추적에 필요

---

### Step 2 — Infrastructure Config 확장

**[2-1] `BuildingStatsConfig` 구조체에 AttackRange 필드 추가**
- 파일: `Assets/_Project/Scripts/Infrastructure/Config/BuildingStatsConfig.cs`
- `BuildingTypeEntry`에 `humanAttackRange`, `spiritAttackRange`, `transcendenceAttackRange` 추가
- 기존 AttackPower/Cooldown 필드 옆에 배치하여 Inspector에서 함께 확인 가능하도록

**[2-2] `GameBootstrapper`에 AttackRange 매핑 추가**
- 파일: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `InitializeBuildingStatsFromConfig()` 메서드에서 StatValues 생성 시 AttackRange 값도 함께 매핑

---

### Step 3 — TowerCombatUseCase 신규 생성

- 파일: `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` (신규)

**역할**: 모든 아군 타워를 순회하며 사거리 내 적 유닛을 탐색하고 쿨다운마다 데미지 적용

**핵심 동작 흐름**:
1. `Tick(float dt)` 호출 → 모든 AutoTower 타입 건물을 순회
2. 각 타워의 `AttackCooldownRemaining`을 `dt`만큼 감소
3. 쿨다운이 0이 되면 → 사거리(AttackRange 타일) 내 가장 가까운 적 유닛 탐색
4. 적이 있으면 → `IDamageable.TakeDamage()` 적용 + 쿨다운 리셋
5. 적 유닛이 사망하면 → `GameEvents.OnUnitDied` 발행 (기존 `ExecuteAttack` 패턴 재사용)

**의존성**:
- `BuildingPlacementUseCase` — 타워 목록 조회
- `UnitSpawnUseCase` — 모든 적 유닛 목록 조회
- `IHexCoordinateMapper` — 타일→월드 좌표 변환 (거리 계산)

**멀티플레이 처리**:
- 싱글플레이: `GameBootstrapper.Update()`에서 `TowerCombatUseCase.Tick(dt)` 직접 호출
- 멀티플레이: `NetworkCombatController`의 서버 Update에서 `TowerCombatUseCase.Tick(dt)` 호출 (서버만)

---

### Step 4 — GameBootstrapper 연결

- 파일: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- TowerCombatUseCase 인스턴스 생성 (생성자 주입)
- `GetTowerCombat()` 접근자 추가 (NetworkCombatController에서 참조 가능하도록)
- 싱글플레이 Update에서 `_towerCombat.Tick(Time.deltaTime)` 호출

---

### Step 5 — NetworkCombatController 멀티플레이 연결

- 파일: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- 서버 `Update()` → `TickCombat(elapsed)` 내부에 타워 Tick 추가:
  ```
  TowerCombatUseCase tower = _bootstrapper.GetTowerCombat();
  tower?.Tick(elapsed);
  ```
- 타워가 유닛을 처치하면 기존 `OnUnitDied` 이벤트 → `EntityDiedClientRpc` 흐름이 자동으로 동작
  (TowerCombatUseCase가 `UnitCombatUseCase.ExecuteAttack`과 동일한 이벤트를 발행하므로)

---

### Step 6 — Inspector 작업 (에디터 스크립트)

`BuildingStatsConfig.asset`에 AutoTower 항목을 입력해야 합니다.
아래 값을 Inspector에서 직접 추가하거나, 1회성 에디터 스크립트로 자동 입력합니다.

| 항목 | Human | Spirit | Transcendence |
|------|-------|--------|---------------|
| HP | 50 | 150 | 100 |
| 건설비용 | 150 | 200 | 175 |
| 공격력 | 15 | 15 | 15 |
| 공격 사거리 | 4.0 | 4.0 | 4.0 |
| 공격 쿨다운 | 5.0 | 3.5 | 5.0 |

---

## 수정 파일 목록

| 파일 | 변경 종류 | 내용 요약 |
|------|----------|---------|
| Domain/Building/BuildingStats.cs | 수정 | StatValues에 AttackRange 추가, GetAttackRange 조회 메서드 추가 |
| Domain/Building/BuildingData.cs | 수정 | AttackCooldownRemaining 런타임 필드 추가 |
| Infrastructure/Config/BuildingStatsConfig.cs | 수정 | BuildingTypeEntry에 attackRange 3종족 필드 추가 |
| Bootstrap/GameBootstrapper.cs | 수정 | AttackRange 매핑, TowerCombatUseCase 생성·연결·Tick |
| Application/UseCases/TowerCombatUseCase.cs | **신규** | 타워 공격 UseCase |
| Infrastructure/Network/NetworkCombatController.cs | 수정 | 서버 Tick에 타워 처리 추가 |
| Resources/Config/BuildingStatsConfig.asset | Inspector 작업 | AutoTower 종족별 스탯 값 입력 |

---

## 위험 요소 및 주의사항

### 위험 1 — BuildingData에 런타임 상태 추가
BuildingData는 Domain 순수 C# 클래스입니다. `AttackCooldownRemaining`을 추가해도 레이어 위반은 없습니다 (UnitData.AttackCooldownRemaining과 동일한 패턴). 단, 건물 생성 시 초기값이 올바르게 0으로 설정되어야 합니다.

### 위험 2 — 기존 BuildingStats.StatValues 확장
StatValues는 struct(값 타입)입니다. 필드를 추가할 때 기존 Initialize 호출 코드에서 AttackRange를 누락하면 모든 타워의 사거리가 0이 되어 공격이 동작하지 않습니다. GameBootstrapper 매핑 코드에서 반드시 함께 추가해야 합니다.

### 위험 3 — 멀티플레이에서 타워 데미지 이중 처리 방지
NetworkCombatController가 서버에서만 TowerCombatUseCase.Tick을 호출해야 합니다. 클라이언트에서도 Tick이 호출되면 타워가 데미지를 두 번 줍니다. IsServer 가드를 명확히 해야 합니다.

### 위험 4 — 타워 공격 애니메이션
이 작업에서는 데미지 로직만 구현합니다. 타워 공격 애니메이션(발사체 이펙트 등)은 별도 작업으로 분리합니다. 타워 시각적 표현 없이 데미지만 적용되는 상태가 이번 작업의 완료 조건입니다.

---

## 완료 조건

1. 싱글플레이에서 AutoTower를 배치하면 사거리 4.0 타일 내 적 유닛에게 쿨다운마다 데미지가 들어간다
2. 종족별로 HP / 비용 / 공격 쿨다운이 StatsReference.md 기준으로 정상 적용된다
3. 멀티플레이(Host + Client 구성)에서도 타워 공격이 서버 권위로 처리된다
4. 타워에게 공격받은 유닛이 HP 0이 되면 정상적으로 사망 처리된다
