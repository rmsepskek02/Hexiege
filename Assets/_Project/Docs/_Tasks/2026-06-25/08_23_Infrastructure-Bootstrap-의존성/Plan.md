# Plan — Infrastructure → Bootstrap 역방향 의존성 제거

## 이 작업이 무엇이고, 왜 하는가 (자연어 설명)

네트워크 관련 코드(`NetworkXxx.cs`) 9개가 `GameBootstrapper`를 직접 이름으로 참조하고 있어,
아키텍처 계층 규칙(Infrastructure는 Bootstrap을 몰라야 한다)을 위반하고 있습니다.

이 작업은 그 참조를 **인터페이스(IGameServices)** 로 교체하여 계층 규칙을 복원합니다.
게임의 동작·결과는 전혀 변하지 않으며, 코드 구조만 올바르게 정렬됩니다.

---

## ⚠️ GameSystemRules 근거

이 작업은 **게임플레이 동작 변경이 아닌 구조 개선**이다.
`StartNetworkGame` / UseCase 획득 경로가 `_bootstrapper.GetXxx()` → `_services.GetXxx()`로 바뀔 뿐,
실제로 반환되는 UseCase 인스턴스와 그 동작은 동일하다.
변경되는 GameSystemRules 규칙은 없다.

---

## 선택한 해결 방법 — Application 계층 서비스 로케이터 패턴

### 왜 이 방법인가

Unity의 `FindFirstObjectByType<T>()`는 인터페이스 타입 T를 지원하지 않아,
Infrastructure 파일이 `GameBootstrapper`를 직접 찾을 수밖에 없는 구조이다.

이를 완전히 해결하려면 Bootstrap 쪽에서 먼저 "저 여기 있어요"라고 등록하고,
Infrastructure는 그 등록된 인터페이스를 꺼내 쓰는 방식이 필요하다.
이를 **서비스 로케이터** 패턴이라 한다.

```
의존 방향 (수정 후)
Bootstrap   → Application  (IGameServices 구현 + 로케이터에 등록)
Infrastructure → Application  (로케이터에서 IGameServices 꺼내 사용)

Bootstrap   ↛  Infrastructure  (역방향 의존 제거)
Infrastructure ↛  Bootstrap    (역방향 의존 제거)
```

---

## 신규 파일 2개

### 1. `Assets/_Project/Scripts/Application/Interfaces/IGameServices.cs`

Infrastructure에서 실제로 사용하는 멤버 12개만 선언.
(GetConfig, GetGameEndUI, GetFlowFieldService는 Infrastructure 미사용 → 제외)

```csharp
namespace Hexiege.Application
{
    // Infrastructure 계층의 NetworkXxx 파일들이 GameBootstrapper를 직접 참조하는 대신
    // 이 인터페이스만 의존하도록 한다. Bootstrap은 이 인터페이스를 구현한다.
    public interface IGameServices
    {
        // UseCase 접근 (Infrastructure Network 파일 실사용 10종)
        HexGrid GetGrid();
        ResourceUseCase GetResource();
        BuildingPlacementUseCase GetBuildingPlacement();
        UnitProductionUseCase GetUnitProduction();
        UnitSpawnUseCase GetUnitSpawn();
        PopulationUseCase GetPopulation();
        UnitMovementUseCase GetMovement();
        UnitCombatUseCase GetCombatUseCase();
        TowerCombatUseCase GetTowerCombat();
        UnitFactory GetUnitFactory();

        // 네트워크 게임 생명주기 (NetworkGameFlow 전용)
        void StartNetworkGame(TeamId localTeam);
        bool IsNetworkGameStarted { get; }
    }
}
```

> **주의**: `HexGrid`는 Domain 타입, UseCase들은 Application 타입, `UnitFactory`는 Infrastructure 타입.
> Infrastructure → Application 인터페이스 참조가 가능한지 `UnitFactory` 네임스페이스 확인 후 using 추가 필요.
> (game-programmer 에이전트가 구현 시 확인)

### 2. `Assets/_Project/Scripts/Application/Services/GameServicesLocator.cs`

```csharp
namespace Hexiege.Application
{
    // Bootstrap이 Register(this)를 호출해 IGameServices를 등록하면,
    // Infrastructure에서 Current를 통해 Bootstrap 없이 접근 가능.
    public static class GameServicesLocator
    {
        public static IGameServices Current { get; private set; }

        // Bootstrap의 Awake()에서 호출
        public static void Register(IGameServices services) => Current = services;

        // Bootstrap의 OnDestroy()에서 호출 (씬 전환 후 stale 참조 방지)
        public static void Unregister() => Current = null;
    }
}
```

---

## 수정 파일 1 — `GameBootstrapper.cs`

### 변경 내용

1. `public partial class GameBootstrapper : MonoBehaviour` 선언에 `IGameServices` 구현 추가:
   ```csharp
   public partial class GameBootstrapper : MonoBehaviour, IGameServices
   ```

2. `Awake()` 추가 — Register 전용 (Start보다 먼저 실행되어 OnNetworkSpawn 전에 등록 보장):
   ```csharp
   private void Awake()
   {
       // IGameServices를 Application 계층 로케이터에 등록.
       // OnNetworkSpawn()이 실행되기 전(Start보다 앞서)에 등록되어야 한다.
       GameServicesLocator.Register(this);
   }
   ```

3. `OnDestroy()` 추가 — 씬 전환 시 stale 참조 방지:
   ```csharp
   private void OnDestroy()
   {
       GameServicesLocator.Unregister();
   }
   ```

4. using 추가:
   ```csharp
   using Hexiege.Application;
   ```

> 기존 `Start()` 코드는 **완전 무수정**. 단순히 `Awake()` + `OnDestroy()` 추가.

---

## 수정 파일 2~10 — Infrastructure Network 파일 9개 (공통 패턴)

### 각 파일에서 수행할 변경 (3단계)

#### 단계 A: 필드 타입 변경
```csharp
// Before
private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

// After
private IGameServices _services;
```

#### 단계 B: OnNetworkSpawn 교체
```csharp
// Before
_bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
if (_bootstrapper == null)
    Debug.LogWarning("...");

// After
_services = GameServicesLocator.Current;
if (_services == null)
    Debug.LogWarning("... GameServicesLocator에 IGameServices가 등록되지 않았습니다.");
```

#### 단계 C: 사용 지점 일괄 치환
```csharp
// Before
_bootstrapper.GetResource()   →   _services.GetResource()
_bootstrapper.GetUnitSpawn()  →   _services.GetUnitSpawn()
// ... 나머지 멤버 동일하게 치환
```

> 필드명 `_bootstrapper` → `_services`로 일괄 치환(rename)하여 누락 방지.
> Bootstrap namespace를 직접 import하는 `using Hexiege.Bootstrap;` 라인이 있으면 제거.
> (실제로는 9개 파일 모두 `using Hexiege.Bootstrap;` 없이 풀네임으로 참조 중 — 풀네임만 제거하면 됨)

---

## 수정 파일 목록 요약

```
[신규]
- Assets/_Project/Scripts/Application/Interfaces/IGameServices.cs
- Assets/_Project/Scripts/Application/Services/GameServicesLocator.cs

[수정]
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs
    · : MonoBehaviour, IGameServices 추가
    · Awake() + OnDestroy() 추가
    · using Hexiege.Application; 추가
- Assets/_Project/Scripts/Infrastructure/Network/NetworkBuildingController.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkHealthSync.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkResourceSync.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkTileSync.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs
```

총 **신규 2개 + 수정 10개** = 12개 파일.

---

## 구현 순서 (game-programmer 에이전트 수행)

1. `IGameServices.cs` 신규 작성 (Application/Interfaces)
   - using 정리: 반환 타입에 필요한 `Hexiege.Domain`, `Hexiege.Application` 등 확인
2. `GameServicesLocator.cs` 신규 작성 (Application/Services)
   - Services 폴더 없으면 신규 생성
3. `GameBootstrapper.cs` 수정 — `IGameServices` 구현 선언 + `Awake` + `OnDestroy`
4. Infrastructure 9개 파일 수정 — 공통 패턴 적용 (필드 타입 + OnNetworkSpawn + 사용 지점)

---

## 위험 요소 및 대응

| 위험 | 대응 |
|------|------|
| `GameServicesLocator.Current == null` 타이밍 (GameBootstrapper.Awake 전에 OnNetworkSpawn 실행) | GameBootstrapper가 MonoBehaviour이고 씬 시작 시 Awake가 먼저 실행되므로 NGO 스폰 이전에 등록됨. 주의: Test 씬 등 특수 환경에서는 순서 확인 필요 |
| `UnitFactory` 네임스페이스 (`Hexiege.Infrastructure`)가 Application 인터페이스에 포함 | Application → Infrastructure 의존을 만들어 역방향. 해결책: `UnitFactory`를 Application/Domain 계층으로 이동하거나, `GetUnitFactory()` 대신 `IUnitFactory` 인터페이스 사용. **game-programmer 에이전트가 실제 네임스페이스 확인 후 결정** |
| `Awake` 중복 정의 (partial class의 다른 파일에 Awake 있을 수 있음) | GameBootstrapper.cs가 partial class → 다른 파일에 `Awake` 없는지 확인 필요 |
| 9개 파일의 `_bootstrapper` 치환 누락 | 각 파일 수정 후 `Hexiege.Bootstrap` 문자열이 남아있지 않은지 grep으로 확인 |

---

## 검증 기준 (구현 후)

- `grep -r "Hexiege.Bootstrap" Assets/_Project/Scripts/Infrastructure/` 결과 0건.
- 컴파일 에러 없음.
- 멀티플레이 게임 시작 → 양 팀 화면 정상 로드 (기존 MULTI-TC-01/02와 동일).
- 건물 조작, 유닛 생산, 전투 등 네트워크 동기화 정상 동작.

> 본 검증은 사용자가 명시적으로 TC/QA를 요청한 경우에만 Testcase.md로 진행한다 (WORKFLOW.md [5-1]).

---

## 사용자 결정 필요 항목

1. **`UnitFactory` 처리 방법** — `GetUnitFactory()` 반환 타입이 `Infrastructure.Factories.UnitFactory`인 경우,
   이를 Application 인터페이스에 포함하면 Application → Infrastructure 역방향 의존이 생긴다.
   → (A) `IUnitFactory` 인터페이스를 Application에 추가하고 `UnitFactory`가 구현 / (B) `UnitFactory`를 Application 계층으로 이동 / (C) 현재 구조 허용(실용주의)
   → **game-programmer 에이전트가 `UnitFactory` 실제 네임스페이스 확인 후 옵션 제시**.

2. **`Awake` 중복 확인** — partial class 다른 파일에 `Awake` 정의가 없는지 구현 전 확인 필요.
