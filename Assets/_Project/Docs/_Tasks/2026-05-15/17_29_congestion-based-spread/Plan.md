# Plan — 혼잡도 기반 유닛 분산 (Congestion-Based Spread)

**상태:** ✅ 완료 (2026-05-15)

**완료 요약:**
- CongestionConfig 별도 ScriptableObject 미생성 → GameConfig에 `CongestionDecayInterval`, `CongestionWeight` 필드 통합
- CastleApproachManager(v1)는 `#if false` 비활성화 후 테스트 완료 시점에 최종 삭제 완료
- 사용자 테스트 결과: 분산 효과 확인, `CongestionDecayInterval` / `CongestionWeight` 수치 조정으로 밸런스 튜닝 가능

---

## 작업 목적 (자연어 설명)

유닛이 적 성을 향해 이동할 때 세로로 줄 서는 현상을 없애기 위해, 타일마다 "얼마나 많은 유닛이 지나갔는지"를 기록하고, 다음 유닛의 경로 계산 시 많이 지나간 타일을 더 비싼 경로로 처리합니다.

예를 들어 유닛 1이 중앙 경로를 지나가면 그 타일들의 혼잡도가 올라갑니다. 이후 생산된 유닛 2는 중앙 경로가 비싸졌으므로 다른 경로를 선택하게 됩니다. 일정 시간이 지나면 혼잡도가 낮아져 자연스럽게 균형을 찾아갑니다. 이 방식은 맵 구조와 무관하게 유닛 통행량에 반응하여 자동으로 분산 효과를 냅니다.

---

## ⚠️ 기존 로직 비활성화 (최상단 명시)

v1(CastleApproachManager) 방식은 시각적으로 효과가 없어 v2로 교체합니다.
**신규 구현 테스트 통과 후 [6] 사용자 테스트 완료 시점에 최종 삭제합니다. 지금은 주석 처리(비활성화)만 합니다.**

### 비활성화 대상

**`CastleApproachManager.cs`**
- 클래스 전체를 `#if false / #endif` 로 감싸 비활성화

**`ProductionTicker.cs` 주석 처리 항목:**
- `private CastleApproachManager _castleApproachManager;` 필드
- `Initialize()` 의 `CastleApproachManager castleApproach` 파라미터 및 저장 코드
- `SiegeEntry` 의 `public HexCoord ApproachTile;` 필드
- `MoveTowardEnemyCastle()` 내 `AssignApproachTile` 호출 및 approachTile 관련 코드
- `RegisterSiege()` 의 approachTile 파라미터 및 저장 코드
- `TickSiege()` 내 `entry.ApproachTile` 사용 코드
- `OnEntityDied` 구독 내 `_castleApproachManager?.Release(unit.Id)` 호출

**`GameBootstrapper.cs` 주석 처리 항목:**
- `private CastleApproachManager _castleApproachManager;` 필드
- `CreateUseCases()` 내 `new CastleApproachManager(_grid)` 생성 코드
- `SetupProduction()` 내 `Initialize()` 호출의 `_castleApproachManager` 인자
- `ClearAll()` 내 `_castleApproachManager?.Clear()`

---

## 구현 계획

### [1] 신규: `CongestionConfig.cs` + `CongestionConfig.asset`

**파일 위치:**
- `Assets/_Project/Scripts/Infrastructure/Config/CongestionConfig.cs`
- `Assets/_Project/Resources/Config/CongestionConfig.asset`

**역할:** Inspector에서 조정 가능한 혼잡도 설정값을 담는 ScriptableObject.

```csharp
[CreateAssetMenu(menuName = "Hexiege/Config/CongestionConfig")]
public class CongestionConfig : ScriptableObject
{
    [Tooltip("혼잡도가 1 감소하는 간격(초). 작을수록 빠르게 사라짐.")]
    public float DecayInterval = 5f;

    [Tooltip("혼잡도 1당 경로 비용 추가량. 클수록 혼잡 타일을 더 강하게 회피.")]
    public float CongestionWeight = 3f;
}
```

---

### [2] 신규: `CongestionMap.cs`

**파일 위치:** `Assets/_Project/Scripts/Application/Services/CongestionMap.cs`

**레이어:** Application — 순수 C# 클래스 (MonoBehaviour 아님)

**역할:** 타일별 혼잡도를 추적하고 감쇠를 처리한다.

**주요 내용:**
- `Dictionary<HexCoord, int> _congestion` — 타일별 혼잡도 카운트
- `void Increment(HexCoord tile)` — 유닛이 타일에 진입할 때 호출, 혼잡도 +1
- `int Get(HexCoord tile)` — 특정 타일의 현재 혼잡도 반환 (없으면 0)
- `void Decay()` — 모든 타일 혼잡도 -1, 0 이하 항목은 Dictionary에서 제거
- `void Clear()` — 재경기 시 전체 초기화

**감쇠 호출 방식:**
- ProductionTicker가 `CongestionConfig.DecayInterval` 간격 타이머를 관리하여 `CongestionMap.Decay()`를 주기적으로 호출

---

### [3] 신규: `CongestionAwarePathfinder.cs`

**파일 위치:** `Assets/_Project/Scripts/Application/Services/CongestionAwarePathfinder.cs`

**레이어:** Application — 순수 C# 클래스

**역할:** 혼잡도를 반영한 가중치 A*로 유닛별 개별 경로를 계산한다.

**알고리즘:**
- A* (우선순위 큐 기반)
- 타일 비용 = 기본 1 + (혼잡도 × CongestionWeight)
- 휴리스틱 = 현재 타일에서 목적지까지 헥스 거리
- HexGrid를 참조하여 walkable 여부 확인

**주요 메서드:**
```csharp
// 혼잡도를 반영한 경로 반환. 경로 없으면 빈 리스트.
public List<HexCoord> FindPath(
    HexCoord start,
    HexCoord destination,
    HexGrid grid,
    CongestionMap congestion,
    float congestionWeight)
```

---

### [4] 수정: `ProductionTicker.cs`

#### 4-1. 필드 추가
```csharp
private CongestionMap _congestionMap;
private CongestionConfig _congestionConfig;
private CongestionAwarePathfinder _congestionPathfinder;
private float _decayTimer;
```

#### 4-2. Initialize() 파라미터 추가
```csharp
public void Initialize(..., CongestionMap congestionMap, CongestionConfig congestionConfig, CongestionAwarePathfinder congestionPathfinder)
```

#### 4-3. Tick()에 감쇠 타이머 추가
```csharp
_decayTimer += deltaTime;
if (_decayTimer >= _congestionConfig.DecayInterval)
{
    _decayTimer = 0f;
    _congestionMap.Decay();
}
```

#### 4-4. MoveTowardEnemyCastle() 수정 (핵심)

기존 FlowField 경로 계산 대신 혼잡도 기반 A* 사용:
```csharp
var path = _congestionPathfinder.FindPath(
    unit.Position,
    enemyCastle.Value,
    _grid,
    _congestionMap,
    _congestionConfig.CongestionWeight);

if (path == null || path.Count == 0)
{
    // 폴백: 기존 FindPathToNearestEmptyTile 유지
}
```

#### 4-5. 건물 변경 시 경로 재계산 (GameSystemRules 규칙 4)

건물 건설/파괴 이벤트 구독 → 현재 A* 이동 중인 siege 유닛 전체 경로 재계산 트리거

---

### [5] 수정: `UnitView.cs`

#### 5-1. 타일 진입 시 혼잡도 증가 이벤트 발행

유닛이 A* 이동 중 새 타일에 도달할 때 (타일 전환 완료 시점):
```csharp
if (_isAStarMoving)
{
    GameEvents.OnUnitEnteredTile?.Invoke(_unitData.Id, newTileCoord);
}
```

#### 5-2. 전투 진입(추격 시작) 시 플래그 해제
```csharp
_isAStarMoving = false;
```

#### 5-3. A* 재개 시 플래그 재설정 + 경로 재계산 요청
```csharp
_isAStarMoving = true;
// ProductionTicker 또는 이벤트를 통해 경로 재계산 요청
```

---

### [6] 신규 이벤트: `GameEvents.OnUnitEnteredTile`

**위치:** `GameEvents.cs` 에 추가

```csharp
public static Action<int, HexCoord> OnUnitEnteredTile;
// int: unitId, HexCoord: 진입한 타일 좌표
```

`GameBootstrapper`에서 이 이벤트를 `CongestionMap.Increment()`에 연결한다.

---

### [7] 수정: `GameBootstrapper.cs`

**추가 사항:**
- `CongestionConfig` 로드 (`Resources.Load<CongestionConfig>("Config/CongestionConfig")`)
- `CongestionMap` 인스턴스 생성 (`new CongestionMap()`)
- `CongestionAwarePathfinder` 인스턴스 생성
- `GameEvents.OnUnitEnteredTile` 구독 → `CongestionMap.Increment()` 연결
- `_productionTicker.Initialize()`에 세 인스턴스 주입
- `ClearAll()`에 `_congestionMap.Clear()` 추가

---

## 위험 요소

| 상황 | 처리 방법 |
|------|-----------|
| A* 경로 없음 (모든 경로 차단) | `FindPath`가 빈 리스트 반환 → 기존 `FindPathToNearestEmptyTile` 폴백 유지 |
| 혼잡도가 너무 높아 비정상 우회 | `CongestionWeight` 낮추거나 `DecayInterval` 줄여서 튜닝 |
| 유닛 수 증가에 따른 A* 성능 저하 | 스폰 시 1회 계산, 재계산은 전투 재개 및 건물 변경 시에만 |
| 멀티플레이 동기화 | 혼잡도 관리 및 경로 계산은 서버 전용. 클라이언트는 NetworkTransform으로 결과만 수신 |

---

## Inspector 조정 가이드

`CongestionConfig.asset`에서 두 값을 튜닝한다:

| 파라미터 | 기본값 | 효과 |
|---------|--------|------|
| `DecayInterval` | 5초 | 낮추면 혼잡도가 빨리 사라져 분산 약해짐, 높이면 오래 유지되어 분산 강해짐 |
| `CongestionWeight` | 3.0 | 낮추면 혼잡 타일도 자주 선택됨, 높이면 강하게 회피 |

---

## 수정할 파일 목록

| 파일 | 수정 유형 |
|------|----------|
| `CastleApproachManager.cs` | `#if false` 전체 비활성화 |
| `ProductionTicker.cs` | v1 주석 처리 + v2 코드 추가 |
| `GameBootstrapper.cs` | v1 주석 처리 + v2 코드 추가 |
| `UnitView.cs` | 타일 진입 이벤트 발행, 전투/A* 재개 플래그 처리 |
| `GameEvents.cs` | `OnUnitEnteredTile` 이벤트 추가 |
| (신규) `CongestionConfig.cs` | ScriptableObject 정의 |
| (신규) `CongestionMap.cs` | 혼잡도 추적 서비스 |
| (신규) `CongestionAwarePathfinder.cs` | 가중치 A* 경로 계산 서비스 |
| (신규) `CongestionConfig.asset` | Inspector 설정 파일 |
