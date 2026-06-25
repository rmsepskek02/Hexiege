# Research — Infrastructure → Bootstrap 역방향 의존성 제거

## 이 작업이 무엇이고, 왜 하는가 (자연어 설명)

현재 네트워크 관련 파일 9개(`NetworkXxx.cs`)가 게임의 "조립 담당" 코드인 `GameBootstrapper`를
직접 이름으로 참조하고 있습니다.

이것이 문제인 이유는 **아키텍처 계층의 규칙** 때문입니다.
이 프로젝트는 Clean Architecture를 따르며, 의존 방향은 항상 "외부 → 내부"여야 합니다.

```
Domain  ←  Application  ←  Infrastructure/Presentation  ←  Bootstrap
```

`GameBootstrapper`는 가장 바깥쪽(Bootstrap 계층)이고,
`NetworkXxx.cs` 파일들은 Infrastructure 계층입니다.
Bootstrap이 Infrastructure에 의존해야 맞는데, 지금은 **반대로** Infrastructure가 Bootstrap에 의존하고 있습니다.

이를 고치면:
- 네트워크 코드와 게임 조립 코드 사이의 결합이 끊어진다.
- 나중에 `GameBootstrapper`를 수정하거나 대체해도 네트워크 파일을 건드릴 필요가 없어진다.
- "인터페이스에만 의존한다"는 아키텍처 원칙이 지켜진다.
- 게임의 동작·결과는 전혀 바뀌지 않는다.

---

## 현재 의존 관계 (Architecture Violation)

```
Infrastructure (NetworkXxx.cs)
    ↓ FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>()
Bootstrap (GameBootstrapper)   ← 역방향 의존 (규칙 위반)
```

위반 형태: 9개 파일 모두 `OnNetworkSpawn()`에서 `Hexiege.Bootstrap.GameBootstrapper`를 전체 경로로 참조한다 (`using Hexiege.Bootstrap;` 없이 네임스페이스 풀네임 사용).

---

## 위반 파일 목록 (9개)

모두 `Assets/_Project/Scripts/Infrastructure/Network/` 하위에 위치.

| 파일 | 사용하는 GameBootstrapper 멤버 |
|------|-------------------------------|
| `NetworkBuildingController.cs` | GetBuildingPlacement(), GetResource(), GetUnitProduction() |
| `NetworkCombatController.cs` | GetUnitSpawn(), GetCombatUseCase(), GetTowerCombat(), GetUnitFactory(), GetBuildingPlacement() |
| `NetworkGameFlow.cs` | **StartNetworkGame(TeamId)**, **IsNetworkGameStarted**, GetResource() |
| `NetworkProductionController.cs` | GetUnitProduction(), GetResource(), GetPopulation(), GetUnitSpawn(), GetUnitFactory() |
| `NetworkHealthSync.cs` | GetUnitSpawn(), GetBuildingPlacement() |
| `NetworkResourceSync.cs` | GetResource() |
| `NetworkTileSync.cs` | GetGrid() |
| `NetworkUnit.cs` | GetUnitFactory() |
| `NetworkUnitMovementController.cs` | GetUnitSpawn(), GetMovement(), GetUnitFactory() |

---

## 공통 패턴

9개 파일 모두 동일한 패턴을 사용한다.

```csharp
// 1. 필드 선언 (풀네임으로 Bootstrap 직접 참조)
private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

// 2. OnNetworkSpawn에서 씬 탐색 후 캐시
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
    if (_bootstrapper == null)
        Debug.LogWarning("...");
}

// 3. 이후 실제 사용 시 캐시된 참조로 UseCase 획득
ResourceUseCase resource = _bootstrapper.GetResource();
```

---

## NetworkGameFlow 특수 케이스

`NetworkGameFlow.cs`는 UseCase 획득 외에 두 가지 특별한 멤버를 추가로 사용한다.

```csharp
// (A) 게임 시작 메서드 호출
_bootstrapper.StartNetworkGame(LocalPlayerTeam.Current);  // Bootstrap.Network.cs:38

// (B) 중복 실행 방지 플래그 확인
if (_bootstrapper.IsNetworkGameStarted) return;           // Bootstrap.cs:332
```

두 멤버 모두 인터페이스에 포함시켜야 한다.

---

## GameBootstrapper의 공개 Get 메서드 전체 목록 (참고)

`GameBootstrapper.cs`(Setup.cs 포함)가 공개하는 멤버:

| 메서드 / 프로퍼티 | Infrastructure에서 사용? |
|-------------------|--------------------------|
| `HexGrid GetGrid()` | ✅ NetworkTileSync |
| `ResourceUseCase GetResource()` | ✅ 여러 곳 |
| `BuildingPlacementUseCase GetBuildingPlacement()` | ✅ 여러 곳 |
| `GameConfig GetConfig()` | ❌ (Infrastructure 미사용) |
| `UnitProductionUseCase GetUnitProduction()` | ✅ 여러 곳 |
| `UnitSpawnUseCase GetUnitSpawn()` | ✅ 여러 곳 |
| `PopulationUseCase GetPopulation()` | ✅ NetworkProductionController |
| `UnitMovementUseCase GetMovement()` | ✅ NetworkUnitMovementController |
| `UnitCombatUseCase GetCombatUseCase()` | ✅ NetworkCombatController |
| `TowerCombatUseCase GetTowerCombat()` | ✅ NetworkCombatController |
| `UnitFactory GetUnitFactory()` | ✅ 여러 곳 |
| `GameEndUI GetGameEndUI()` | ❌ (Infrastructure 미사용) |
| `FlowFieldService GetFlowFieldService()` | ❌ (Infrastructure 미사용) |
| `void StartNetworkGame(TeamId)` | ✅ NetworkGameFlow |
| `bool IsNetworkGameStarted` | ✅ NetworkGameFlow |

→ **인터페이스에 포함할 멤버: 12개** (Infrastructure에서 실제로 사용하는 것만).

---

## Inspector(직렬화) 의존성 없음

9개 파일 중 `[SerializeField]`로 `GameBootstrapper`를 직접 연결하는 파일은 없다.
모두 런타임에 `FindFirstObjectByType<>()`으로 탐색한다.
따라서 이 작업에서 **Unity 씬·프리팹의 Inspector 연결을 다시 맺을 필요가 없다**.

---

## 해결 방향 검토

### 문제: FindFirstObjectByType<T>()는 인터페이스 타입 T를 지원하지 않는다

Unity의 `FindFirstObjectByType<T>()`는 `T`가 `MonoBehaviour` 상속 클래스여야 한다.
인터페이스 타입은 사용할 수 없다.
따라서 단순히 `GameBootstrapper`를 인터페이스로 바꾸는 것만으로는 Infrastructure에서 씬 탐색이 불가능하다.

### 해결책: Application 계층에 서비스 로케이터 추가 (권장)

```
Application/Interfaces/IGameServices.cs   ← 새 인터페이스
Application/Services/GameServicesLocator.cs  ← 새 정적 로케이터
```

1. `IGameServices` — Infrastructure가 사용하는 12개 멤버를 선언하는 인터페이스.
2. `GameServicesLocator` — `IGameServices Current { get; }` 프로퍼티를 노출하는 정적 클래스.
   Application 계층에 위치하므로 Infrastructure도, Bootstrap도 모두 참조 가능.
3. `GameBootstrapper`가 `IGameServices`를 구현하고, `Awake`(또는 `Start`)에서 `GameServicesLocator.Register(this)`를 호출.
4. 9개 Infrastructure 파일은 `FindFirstObjectByType<>()` 대신 `GameServicesLocator.Current`를 사용.
   → Bootstrap 네임스페이스 참조 완전 제거.

```
의존 방향 (수정 후):
Bootstrap  → Application (IGameServices 구현, Register 호출)
Infrastructure → Application (IGameServices 사용, GameServicesLocator.Current 접근)
```

양쪽 모두 Application만 바라보게 되어 역방향 의존이 사라진다.

---

## 영향 범위

- **신규 파일 2개**: `IGameServices.cs`, `GameServicesLocator.cs` (Application 계층)
- **수정 파일**: `GameBootstrapper.cs` (IGameServices 구현 선언 + Register 호출 1~2줄)
- **수정 파일**: 위반 파일 9개 (필드 타입 변경 + FindFirstObjectByType 제거 + using 정리)
- **게임 동작 변경 없음**: 씬 탐색 → 정적 로케이터 조회로 교체이므로 실행 결과 동치.

---

## 작업 시 주의사항 (Plan 단계 결정 대상)

1. `GameServicesLocator.Register(this)` 호출 시점: `Awake` vs `Start` — Bootstrap의 초기화 순서와 충돌 여부 확인 필요.
2. `GameServicesLocator.Unregister()` 호출 필요 여부 — 씬 전환 시 stale 참조 방지.
3. `IGameServices`에 포함할 멤버를 Infrastructure 실사용 12개로 한정할지,
   전체 공개 메서드 15개로 노출할지 (최소 원칙 vs 미래 편의성).
