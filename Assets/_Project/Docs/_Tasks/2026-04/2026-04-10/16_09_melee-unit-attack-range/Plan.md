# Plan: 유닛 타입 개편 + 근접 사거리 시스템 수정

**날짜**: 2026-04-10  
**최종 수정**: 2026-04-10 (설계 확정 후 전면 재작성)

---

## 1. 배경 및 목적

기존 UnitType(Pistoleer/Assault/Sniper)은 종족 공통 카테고리로 설계되었으나,
기획은 **종족별 독립 유닛** 구조임. 따라서 Spirit/Transcendence 유닛들이
Human 유닛 타입에 임시 매핑된 상태로 테스트됐음.

이번 작업에서:
1. UnitType을 **유닛별 독립 식별자**로 개편
2. 각 유닛이 자기 고유 스탯(range 포함)을 갖도록
3. 근접 사거리(range 0.5) 동작이 올바르게 동작하도록 시스템 수정
4. UnitFactory를 새 구조에 맞게 리팩토링

---

## 2. 작업 범위

### 포함
- `UnitType.cs`: 유닛별 독립 enum 값 추가
- `UnitStats.cs`: Spirit/Transcendence 유닛 스탯 추가 (StatsReference 기준)
- `UnitFactory.cs`: 고정 슬롯 구조 → 유닛 타입별 매핑 리스트 구조로 변경
- `UnitMovementUseCase.cs`: 적 Position blocked 제거 (전 유닛 통일)
- `UnitCombatUseCase.cs`: HexCoord 폴백 range < 1.0 버그 수정
- `SetupUnitFactoryPrefabs.cs` (에디터 스크립트): 새 구조에 맞게 프리팹 자동 연결 재작성

- `ProductionPanelUI.cs`: 종족별 유닛 버튼 동적 매핑 (버튼 UnitType + 초상화 종족 대응)
- `HexPathfinder.cs` + `UnitMovementUseCase.cs` + `UnitView.cs`: 근접 유닛 목표 방향 이동 (아래 §9 참조)
- `UnitCombatUseCase.cs`: 잘못 적용된 effectiveRange 수정 되돌리기

### 미포함 (별도 작업)
- 다중 히트 프레임(FlameSpirit 6히트, LionKnight 4히트)
- StatsReference.md의 HP/공격력/생산 비용 등 미정 스탯 입력 (사용자 직접 작성)

---

## 3. UnitType 개편

### 변경 전 (카테고리형)
```csharp
public enum UnitType
{
    Pistoleer = 0,  // 모든 종족 공유
    Assault   = 1,
    Sniper    = 2
}
```

### 변경 후 (유닛별 독립형)
```csharp
public enum UnitType
{
    // ── 인간계 (Human) ──
    Pistoleer   = 0,
    Assault     = 1,
    Sniper      = 2,

    // ── 정령계 (Spirit) ──
    FlameSpirit   = 3,   // range 0.5
    EmberSpirit   = 4,   // range 0.5
    InfernoSpirit = 5,   // range 4.0

    // ── 초월계 (Transcendence) ──
    BearGuard   = 6,   // range 0.5
    FoxMagician = 7,   // range 3.0
    LionKnight  = 8    // range 0.5
}
```

---

## 4. UnitStats 추가 내용

StatsReference.md에서 확인된 값으로 추가.  
HP/공격력/생산비용 등 미정 항목은 플레이스홀더로 작성.

| 유닛 | range | speed | cooldown (클립길이) | HitFrameTime |
|------|-------|-------|---------------------|--------------|
| FlameSpirit | 0.5 | 2.0 | 3.0s (3:00) | 0:20 = 0.667s |
| EmberSpirit | 0.5 | 0.5 | 2.33s (2:20) | 1:00 = 1.000s |
| InfernoSpirit | 4.0 | 1.0 | 3.0s (3:00) | 1:15 = 1.250s |
| BearGuard | 0.5 | 1.0 | 1.33s (1:20) | 0:20 = 0.667s |
| FoxMagician | 3.0 | 0.5 | 4.0s (4:00) | 2:25 = 2.417s |
| LionKnight | 0.5 | 2.0 | 2.33s (2:20) | 0:15 = 0.250s |

> AttackCooldown은 UnitFactory에서 Animator 클립 길이로 덮어씀 (기존 동일).  
> HitFrameTime은 StatsReference의 첫 번째 히트 프레임 값 기준.

---

## 5. UnitFactory 구조 변경

### 변경 전 — 고정 슬롯 구조
```csharp
[System.Serializable]
public struct UnitTeamPrefabSet
{
    public GameObject pistoleer;
    public GameObject assault;
    public GameObject sniper;
}

[SerializeField] private UnitTeamPrefabSet _humanBluePrefabs;
// ... 종족별 6세트
```

### 변경 후 — 유닛 타입별 리스트 구조
```csharp
[System.Serializable]
public struct UnitPrefabEntry
{
    public UnitType type;
    public GameObject blue;
    public GameObject red;
}

[Header("인간계")]
[SerializeField] private List<UnitPrefabEntry> _humanPrefabs;

[Header("정령계")]
[SerializeField] private List<UnitPrefabEntry> _spiritPrefabs;

[Header("초월계")]
[SerializeField] private List<UnitPrefabEntry> _transcendencePrefabs;
```

프리팹 조회 메서드:
```csharp
private GameObject GetPrefab(RaceId race, UnitType type, TeamId team)
{
    var list = race switch {
        RaceId.Human        => _humanPrefabs,
        RaceId.Spirit       => _spiritPrefabs,
        RaceId.Transcendence => _transcendencePrefabs,
        _ => null
    };
    var entry = list?.Find(e => e.type == type);
    return team == TeamId.Blue ? entry?.blue : entry?.red;
}
```

### Inspector 매핑 (에디터 스크립트가 자동 연결)
| Race | UnitType | Blue | Red |
|------|----------|------|-----|
| Human | Pistoleer | Unit_Pistoleer_Blue | Unit_Pistoleer_Red |
| Human | Assault | Unit_Assault_Blue | Unit_Assault_Red |
| Human | Sniper | Unit_Sniper_Blue | Unit_Sniper_Red |
| Spirit | FlameSpirit | Unit_FlameSpirit_Blue | Unit_FlameSpirit_Red |
| Spirit | EmberSpirit | Unit_EmberSpirit_Blue | Unit_EmberSpirit_Red |
| Spirit | InfernoSpirit | Unit_InfernoSpirit_Blue | Unit_InfernoSpirit_Red |
| Transcendence | BearGuard | Unit_BearGuard_Blue | Unit_BearGuard_Red |
| Transcendence | FoxMagician | Unit_FoxMagician_Blue | Unit_FoxMagician_Red |
| Transcendence | LionKnight | Unit_LionKnight_Blue | Unit_LionKnight_Red |

프리팹 경로: `Assets/_Project/Prefabs/Units/{Race}/Unit_{Name}_{Team}.prefab`

---

## 6. UnitMovementUseCase 수정

적 유닛 Position을 blocked에서 **전 유닛 대상으로 제거** (근접 유닛만 분기하지 않음).

```csharp
// 변경 전
foreach (var other in _unitSpawn.Units.Values)
{
    if (other.Id != unit.Id && other.IsAlive)
    {
        blocked.Add(other.Position);  // 아군 + 적군 모두 차단
        if (other.Team == unit.Team && other.ClaimedTile.HasValue)
            blocked.Add(other.ClaimedTile.Value);
    }
}

// 변경 후
foreach (var other in _unitSpawn.Units.Values)
{
    if (other.Id != unit.Id && other.IsAlive)
    {
        // 아군 Position만 차단 (적 유닛은 HasEnemyInRange로 전투 감지하므로 경로에서 제외)
        if (other.Team == unit.Team)
        {
            blocked.Add(other.Position);
            if (other.ClaimedTile.HasValue)
                blocked.Add(other.ClaimedTile.Value);
        }
    }
}
```

이유: 원거리 유닛도 HasEnemyInRange가 적 타일 도달 전에 발동하므로
적 위치 blocked 제거는 실질 동작 변화 없이 코드를 단순화함.

---

## 7. UnitCombatUseCase 수정

HexCoord 폴백 메서드의 range < 1.0 판정 버그 수정.

```csharp
// 변경 전 (FindFirstEnemyTargetByHexCoord 내부)
if (distance <= attacker.AttackRange && distance < minDistance)
// AttackRange = 0.5 → distance <= 0 → 항상 miss (정수 비교)

// 변경 후
int rangeThreshold = Mathf.Max(1, Mathf.CeilToInt(attacker.AttackRange));
if (distance <= rangeThreshold && distance < minDistance)
// AttackRange = 0.5 → threshold = 1 → 인접 타일까지 폴백 탐색
```

---

## 8. 에디터 스크립트 수정

기존 `SetupUnitFactoryPrefabs.cs`를 새 `List<UnitPrefabEntry>` 구조에 맞게 재작성.

- 메뉴: `Hexiege/Setup/UnitFactory 프리팹 연결` (기존 경로 유지)
- 9개 유닛(3종족 × 3유닛) × 2팀 = 18개 프리팹 자동 연결
- 연결 후 `EditorUtility.SetDirty()` + `AssetDatabase.SaveAssets()` 호출

---

## 9. 근접 유닛 목표 방향 이동 시스템 (신규 추가)

### 배경
근접 유닛(range 0.5)의 공격 판정 거리 = `0.5 × 0.866 + 0.05 = 0.483f`.
그런데 현재 경로 탐색은 성 타일이 `IsWalkable = false`이므로 **성 인접 타일에서 경로가 끝남**.
유닛이 인접 타일 중심에서 멈추면 성까지 거리는 0.866f → 0.483f 판정 범위 밖 → 공격 불가.

### 원하는 동작
유닛이 성 인접 타일에 도달한 후에도 **성 타일 방향으로 계속 Lerp 이동**하다가,
성까지 거리가 0.483f 이내가 되는 시점에 공격 시작.
(`maxDist = 0.483f`는 유지, 경로 시스템을 수정)

### 구현 방법

경로 목표가 walkable하지 않은 건물 타일인 경우:
`RequestMove`에서 인접 walkable 타일까지 정상 경로를 찾은 뒤 **원래 목표 타일을 경로 마지막에 추가**.

```csharp
// RequestMove 내부 (UnitMovementUseCase)
List<HexCoord> path = HexPathfinder.FindPath(_grid, unit.Position, target, blocked);

// 목표 타일이 walkable하지 않아 경로가 없는 경우 (예: 성 타일)
// → 인접 walkable 타일까지 경로를 탐색하고, target을 마지막에 추가
if ((path == null || path.Count < 2))
{
    HexTile goalTile = _grid.GetTile(target);
    if (goalTile != null && !goalTile.IsWalkable)
    {
        // HexPathfinder에 인접 타일까지 경로 탐색 요청
        path = HexPathfinder.FindPathToNeighbor(_grid, unit.Position, target, blocked);
        // Count >= 1: 유닛이 이미 최적 인접 타일 위에 있으면 count=1 반환 → 이 경우도 성 타일 추가 필요
        if (path != null && path.Count >= 1)
            path.Add(target);   // 성 타일을 마지막에 추가 → Lerp 방향용
    }
}
```

`UnitView.MoveAlongPath`에서 경로의 마지막 타일이 walkable하지 않은 경우:
- Lerp는 수행 (성 타일 방향으로 이동)
- `ProcessStep` 호출은 생략 (성 타일 소유권 변경 방지)
- **`ClaimedTile = to` 설정도 생략** (성 타일을 ClaimedTile로 설정하면 두 번째 이후 유닛이 성 타일을 blocked로 인식하여 접근 불가)
- 이동 도중 `HasEnemyInRange` = true가 되면 공격 시작 (기존 로직 그대로)

> **ClaimedTile 영향 분석**: ClaimedTile은 `RequestMove` blocked 구성과 `IsTileBlockedBySameTeam` 두 곳에서 읽힘.
> 성 타일은 walkable하지 않아 아군 유닛이 실제 위치할 수 없으므로, 성 타일에 대한 ClaimedTile 차단은 불필요하며 오히려 후속 유닛의 접근을 막는 부작용이 있음.

`HexPathfinder.FindPathToNeighbor`:
- goal이 walkable하지 않을 때, goal의 인접 walkable 타일 중 start에서 가장 가까운 타일을 찾아 그곳까지 경로 반환
- `start == bestCandidate`이면 `[start]` (count=1) 반환 — 유닛이 이미 최적 인접 타일에 있는 경우

### 변경 파일
| 파일 | 변경 내용 |
|------|------|
| `HexPathfinder.cs` | `FindPathToNeighbor()` 메서드 추가 |
| `UnitMovementUseCase.cs` | `RequestMove`에서 `path.Count >= 2` → `>= 1`로 완화 |
| `UnitView.cs` | `MoveAlongPath`에서 마지막 non-walkable 타일에 `ProcessStep` 및 `ClaimedTile` 설정 생략 |
| `UnitCombatUseCase.cs` | 잘못 적용된 `effectiveRange = max(1.0, AttackRange)` 수정 되돌리기 |

---

## 10. 변경 없는 파일

| 파일 | 이유 |
|------|------|
| `UnitData.cs` | AttackRange는 생성자 파라미터로 전달 — 변경 불필요 |
| `UnitCombatUseCase.FindFirstEnemyTarget` (세계좌표) | maxDist = AttackRange × TileHeight + Epsilon 유지 |
| `NetworkCombatController.cs` | TryFindTarget/HasEnemyInRange 경유 — 변경 불필요 |
| `BuildingFactory.cs` | 건물 타입 구조 무관 |

---

## 10. 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| UnitType int 캐스팅 | 코드 곳곳에 `(int)UnitType` 또는 switch/case가 있으면 새 enum 값 추가 시 누락 가능 | 구현 전 전체 grep으로 UnitType 사용처 확인 필수 |
| Inspector 직렬화 | List<UnitPrefabEntry>로 구조 변경 시 기존 Inspector 연결값 초기화 | 에디터 스크립트로 재연결 처리 |
| ProductionState.AutoEntries | UnitType[] 배열 사용 — 새 enum 값이 자동 포함되지 않음 | 생산 UI 작업 시 함께 처리 (이번 범위 외) |
| HitFrameTime 다중 히트 | FlameSpirit(6히트), LionKnight(4히트) — 현재 단일 HitFrameTime만 지원 | 첫 히트 프레임 값만 적용, 다중 히트 구현은 별도 작업 |

---

## 11. 구현 순서 (game-programmer 위임)

1. UnitType 사용처 전체 grep (switch/case, int 캐스팅 등 누락 가능 지점 파악)
2. `UnitType.cs` — enum 값 추가
3. `UnitStats.cs` — Spirit/Transcendence 유닛 스탯 추가
4. `UnitFactory.cs` — List<UnitPrefabEntry> 구조로 변경
5. `UnitMovementUseCase.cs` — blocked 조건 수정
6. `UnitCombatUseCase.cs` — HexCoord 폴백 수정
7. `SetupUnitFactoryPrefabs.cs` — 에디터 스크립트 재작성
