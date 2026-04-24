# Plan — 유닛 스탯 ScriptableObject 전환

## 목표

`UnitStats.cs` / `UnitProductionStats.cs`의 switch 표현식 하드코딩을 제거하고
Unity Inspector에서 수치를 편집할 수 있는 ScriptableObject 기반 구조로 전환.

---

## 접근 방식

### 전체 흐름

```
[UnitStatsConfig.asset]  ← Inspector에서 수치 편집
        ↓ (Resources.Load)
[GameBootstrapper]  →  UnitStats.Initialize(config)
                    →  UnitProductionStats.Initialize(config)
        ↓ (런타임 조회)
[UnitSpawnUseCase / UnitData]  →  UnitStats.GetMaxHp(type) 등 기존 호출 그대로 유지
```

Domain의 `UnitStats` / `UnitProductionStats`는 내부 저장소만 Dictionary로 바뀌며,
**메서드 시그니처는 변경 없음** → `UnitSpawnUseCase`, `UnitData` 코드 수정 불필요.

---

## 구현 단계

### Step 1 — `UnitStatEntry` 직렬화 구조체 작성
**파일:** `Assets/_Project/Scripts/Infrastructure/Config/UnitStatsConfig.cs` (내부 중첩 struct)

```
[Serializable] struct UnitStatEntry
  - unitType        : UnitType
  - maxHp           : int
  - attackPower     : int
  - attackRange     : float
  - detectRange     : float
  - moveSpeed       : float
  - attackCooldown  : float  ← fallback용 (Animator가 있으면 런타임에 덮어씀)
  - hitFrameTimes   : float[]
  - productionTime  : float
  - goldCost        : int
  - populationCost  : int    ← 현재 전 유닛 1 고정, 향후 확장 대비
```

전투 스탯과 생산 스탯을 하나의 Entry에 통합 → 한 파일에서 유닛 1종의 모든 수치를 한눈에 파악 가능.

---

### Step 2 — `UnitStatsConfig : ScriptableObject` 작성
**파일:** `Assets/_Project/Scripts/Infrastructure/Config/UnitStatsConfig.cs`

```
[CreateAssetMenu(menuName = "Hexiege/UnitStatsConfig")]
class UnitStatsConfig : ScriptableObject
  - [SerializeField] List<UnitStatEntry> _stats
  - UnitStatEntry GetEntry(UnitType type)  ← 내부 조회 헬퍼
```

생성 메뉴: `Assets → Create → Hexiege → UnitStatsConfig`
저장 경로: `Resources/Config/UnitStatsConfig.asset`

---

### Step 3 — `UnitStats.cs` 수정 (Domain)
**파일:** `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`

변경 내용:
- `static Dictionary<UnitType, (int maxHp, int attackPower, float attackRange, float detectRange, float moveSpeed, float attackCooldown, float[] hitFrameTimes)> _data` 추가
- `static void Initialize(IEnumerable<UnitStatEntry_Domain> entries)` 추가
  - `UnitStatEntry_Domain`은 Domain 전용 순수 C# 구조체 (Unity 의존 없음)
  - 또는 튜플(ValueTuple)로 직접 받아 Dictionary에 저장
- 기존 switch 메서드 → Dictionary 조회로 교체
  - miss 시: `Debug.LogWarning` + 기본값 반환 (런타임 안전망)
- `UnitProductionStats` 동일 적용

> **아키텍처 유지:** Infrastructure의 `UnitStatsConfig`가 Domain의 struct로 변환하여 전달하므로 Domain에는 Unity 의존이 없음.

---

### Step 4 — `UnitProductionStats.cs` 수정 (Domain)
**파일:** `Assets/_Project/Scripts/Domain/Unit/UnitProductionStats.cs`

Step 3과 동일한 패턴으로 Dictionary 전환 + `Initialize()` 추가.
(전투 스탯과 하나의 Entry로 통합되므로 `Initialize()` 호출은 `UnitStats.Initialize()`와 동시에 처리)

---

### Step 5 — `GameBootstrapper` 수정
**파일:** `Assets/_Project/Scripts/Infrastructure/Bootstrap/GameBootstrapper.cs`

`Awake()` 또는 `Start()` 초반부에 추가:
```
var config = Resources.Load<UnitStatsConfig>("Config/UnitStatsConfig");
UnitStats.Initialize(config);
UnitProductionStats.Initialize(config);
```

기존 GameConfig 로딩 패턴과 동일.

---

### Step 6 — `UnitStatsConfig.asset` 생성 및 현재 수치 입력
**경로:** `Assets/Resources/Config/UnitStatsConfig.asset`

Unity 에디터에서 `Create → Hexiege → UnitStatsConfig`로 생성 후
현재 `UnitStats.cs`의 수치를 그대로 옮겨 입력.

| 유닛 | HP | ATK | Range | DetectRange | MoveSpeed | Cooldown | ProductionTime | GoldCost |
|------|----|----|-------|-------------|-----------|----------|----------------|----------|
| Pistoleer | 30 | 6 | 1.0 | 1.0 | 0.5 | 2.0 | 5 | 50 |
| Assault | 50 | 1 | 2.0 | 2.0 | 1.0 | 0.2 | 10 | 100 |
| Sniper | 30 | 10 | 5.0 | 5.0 | 0.25 | 3.0 | 15 | 200 |
| FlameSpirit | 50 | 2 | 0.5 | 1.0 | 2.0 | 3.0 | 15 | 200 |
| EmberSpirit | 30 | 5 | 0.5 | 1.0 | 0.5 | 2.33 | 5 | 50 |
| InfernoSpirit | 100 | 25 | 4.0 | 4.0 | 1.0 | 3.0 | 30 | 500 |
| BearGuard | 200 | 10 | 0.5 | 1.0 | 1.0 | 1.33 | 25 | 400 |
| FoxMagician | 20 | 8 | 3.0 | 3.0 | 0.5 | 4.0 | 5 | 50 |
| LionKnight | 50 | 9 | 0.5 | 1.0 | 2.0 | 3.0 | 15 | 200 |

`HitFrameTimes`는 Inspector에서 배열로 입력.

---

## 파일별 변경 요약

| 파일 | 변경 |
|------|------|
| `Infrastructure/Config/UnitStatsConfig.cs` | **신규** ScriptableObject + UnitStatEntry 구조체 |
| `Domain/Unit/UnitStats.cs` | switch → Dictionary, `Initialize()` 추가 |
| `Domain/Unit/UnitProductionStats.cs` | switch → Dictionary, `Initialize()` 추가 |
| `Infrastructure/Bootstrap/GameBootstrapper.cs` | `Initialize()` 호출 2줄 추가 |
| `Resources/Config/UnitStatsConfig.asset` | **신규** 에셋 파일 (Inspector에서 수치 입력) |

**변경 없는 파일:** `UnitData.cs`, `UnitSpawnUseCase.cs`, `UnitFactory.cs`

---

## 주의사항

### AttackCooldown 이중 관리
- SO의 `attackCooldown` = Animator 없을 때 fallback
- `UnitFactory.GetAttackClipLength()` → Animator 있으면 런타임에 덮어씀 (기존 동작 유지)
- SO 편집 시 "Animator 클립 길이와 맞춰야 실제 반영" 문구를 Inspector Tooltip에 명시

### HitFrameTimes 배열 편집
- Inspector에서 `Size`(원소 수)를 먼저 바꾼 뒤 각 값을 입력
- FlameSpirit(6개), LionKnight(2개) 등 유닛별 원소 수가 다름
- 단위: 초 (30fps 기준: N프레임 ÷ 30 = 초)

### 멀티플레이 호환성
- `UnitStats.Initialize()`는 서버/클라이언트 모두 `GameBootstrapper.Start()`에서 호출됨
- SO `.asset`은 빌드에 포함되므로 클라이언트도 동일한 값 사용 → 별도 네트워크 동기화 불필요

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| `Initialize()` 호출 전 `GetXxx()` 접근 | Dictionary miss → `Debug.LogWarning` + fallback 0/기본값 반환 |
| `.asset`에 특정 UnitType Entry 누락 | `GetEntry()` miss → 경고 로그로 즉시 인지 가능 |
| `Resources/Config/` 경로 오탈자 | `Resources.Load` 실패 → null 체크로 에러 로그 |
