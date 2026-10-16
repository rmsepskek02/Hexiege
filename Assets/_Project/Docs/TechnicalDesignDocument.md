# Hexiege - 기술 설계서 (Technical Design Document)

공통 기술 스택·아키텍처·데이터·구현 패턴·개발 환경의 원본이다. 네트워크 상세 구조는 [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md), 확정 제품 동작은 [GameSystemRules.md](GameSystemRules.md), 현재 구현·검증 상태는 [PROJECT_STATUS.md](PROJECT_STATUS.md)에서 관리한다. 날짜별 검증 이력은 현행 기술 사양과 구분한다.

날짜별 구현·native·빌드 시도 원문은 [구현·검증 이력](History/ImplementationVerificationHistory.md#tdd-handoff)에 보존했다. 현재 시스템 요약은 [PROJECT_STATUS.md](PROJECT_STATUS.md), 에셋 연결·개별 수용은 [UnitCombatAssetMatrix.md](Assets/UnitCombatAssetMatrix.md)와 [VFXSFXList.md](Assets/VFXSFXList.md)에서 확인한다. native PASS·빌드 시도·실기 수용·전체 구조 이행은 별도 상태다.
**버전:** 0.62.0
**최종 수정일:** 2026-10-13
**작성자:** HANYONGHEE

> **2026-08-12:** **MistShrine 물안개 힐 구현 완료 — 에디터 싱글플레이 실기 검증 완료 / ⚠️ 멀티플레이 미검증.** 신규 구성 요소: Application `MistShrineUseCase`(물안개 인스턴스 목록 + 물안개별 독립 누적기 + 매 틱 대상 재수집. **HoT/DoT 시간 지속 효과 목록을 사용하지 않아** 자연회복·BloomFairy 힐과의 채널 충돌이 구조적으로 차단된다) · Application `INetworkMistShrineController` / Infrastructure `NetworkMistShrineController`(`Request → ServerRpc(팀 검증) → ClientRpc`, `ResolveServices()` 지연 재조회) · `NetworkHealthSync` **건물 힐 전용 RPC 신설**(기존 유닛 힐 RPC 시그니처 무변경) · Domain `BuildingData.Heal(int)`(프로젝트 최초의 건물 회복 경로) · Presentation `MistShrinePanelUI` / `MistShrineRangeIndicator`. **`PopupClosedFrame` 패턴의 기존 결손 보정** — `InputHandler`의 `ClosedFrame` 가드에 연구 패널·스킬 패널이 누락돼 있던 것을 신규 MistShrine 패널과 함께 등록했다(모든 팝업이 가드에 들어와야 패턴이 성립한다). **⚠️ 멀티 고유 경로(건물 HP 동기화·클라 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱)는 실행된 적이 없다.** 상세: `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`.
> **2026-08-10:** 건물 타입 주석 오류 정정 — `AutoTower`×3종족의 Transcendence는 **VineTower**다(이전 "Trans=MistShrine"은 오류). MistShrine은 방어 타워가 아니라 별도 힐 건물 `HealShrine`(= 6)이며, 물안개 지속 힐로 재설계 확정되었다(**당시 기준 기획 확정 / 구현 미착수 — 2026-08-12 구현 완료**). 규칙: `GameSystemRules/GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템.

---

<a id="목차"></a>

## 📋 목차

1. [기술 스택](#기술-스택)
2. [프로젝트 아키텍처](#프로젝트-아키텍처)
3. [네트워크 설계](#네트워크-설계)
4. [백엔드 설계](#백엔드-설계)
5. [데이터베이스 스키마](#데이터베이스-스키마)
6. [디자인 패턴](#디자인-패턴)
7. [육각형 그리드 시스템](#육각형-그리드-시스템)
8. [AI 시스템](#ai-시스템)
9. [성능 최적화](#성능-최적화)
10. [개발 환경](#개발-환경)

---

<a id="기술-스택"></a>

## 🛠️ 기술 스택

### 핵심 기술
| 항목 | 기술 | 버전 |
|------|------|------|
| **게임 엔진** | Unity | 6000.0.x (Unity 6 LTS) |
| **렌더 파이프라인** | URP | Universal Render Pipeline |
| **네트워크** | Netcode for GameObjects | 2.9.2 |
| **전송 레이어** | Unity Transport (UTP) | - |
| **NAT 관통** | Unity Relay | - |
| **매칭** | Unity Lobby | - |
| **인증** | Firebase Authentication + Google Play Games Plugin | Firebase SDK v13.11.0 + GPGS v2.1.0 설치 완료 (런타임 설정 미완료) |
| **경로찾기** | 커스텀 A* (HexPathfinder) | 자체 구현 |
| **백엔드** | Firebase (Firestore + Functions + Google Play Billing) | - |
| **이벤트 시스템** | UniRx | 7.1.0 |
| **애니메이션** | Animator (Mecanim) | Walk/Attack/Dead 상태 기반 |
| **모바일 입력** | Lean Touch+ / Unity Input System | - |

### 개발 언어
- **C# 9.0** (Unity 6)

### 개발 도구
- **IDE**: Visual Studio 2022 / Rider
- **버전 관리**: Git + GitHub
- **빌드**: Unity Cloud Build (선택)
- **분석**: Firebase Analytics (선택)

---

<a id="프로젝트-아키텍처"></a>

## 🏛️ 프로젝트 아키텍처

### Clean Architecture 구조

```
┌─────────────────────────────────────┐
│      Presentation Layer             │  ← MonoBehaviours, UI, Input
│  (Unity 의존성)                     │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│      Application Layer              │  ← Use Cases, Business Logic
│  (순수 C# + UniRx)                  │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│         Domain Layer                │  ← Entities, Value Objects
│  (순수 C#, Unity 독립)              │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│    Infrastructure Layer             │  ← Network, DB, External APIs
│  (Netcode, PlayFab, 외부 연동)      │
└─────────────────────────────────────┘
```

### 폴더 구조
```
Assets/
└── _Project/
    ├── Scripts/
    │   ├── Bootstrap/           # Composition Root (GameBootstrapper partial × 4, LoginBootstrapper)
    │   ├── Domain/              # 순수 C# 엔티티
    │   ├── Application/         # Use Cases, GameEvents, NetworkContext, Interfaces
    │   ├── Infrastructure/      # 외부 연동, Network Controllers, Factories
    │   ├── Presentation/        # Unity UI/View, UnitView, CameraController
    │   ├── Core/                # 공통 유틸리티 (HexMetrics, ViewConverter)
    │   └── Diagnostics/         # 진단/디버그 전용 도구
    ├── Prefabs/
    ├── Materials/
    ├── Scenes/
    └── Resources/
```

### 의존성 방향 추상화 (Application 인터페이스 패턴)

Clean Architecture에서는 **안쪽 레이어가 바깥쪽 레이어를 알면 안 된다**(역방향 의존 금지).
이를 지키기 위해, 바깥 레이어가 안쪽에 무언가를 제공해야 할 때는
**안쪽 레이어에 인터페이스를 선언하고 바깥 레이어가 그 인터페이스를 구현**한다(의존성 역전).

본 프로젝트에서 이 패턴을 적용한 대표 사례:

| 인터페이스 (선언 위치) | 구현체 (위치) | 해소한 역방향 의존 |
|----------------------|--------------|------------------|
| `IGameServices` (Application) | `GameBootstrapper` (Bootstrap) | Infrastructure/Network → Bootstrap 직접 참조 제거. NetworkXxx가 `FindFirstObjectByType<GameBootstrapper>()` 대신 `IGameServices`로 UseCase에 접근 |
| `IUnitFactory` (Application) | `UnitFactory` (Infrastructure) | `IGameServices.GetUnitFactory()`가 구체 클래스 `UnitFactory`(Infrastructure)를 반환하면 생기는 Application → Infrastructure 역방향 의존 제거. 인터페이스를 반환하도록 변경 |
| `IEntityPositionProvider` (Application) | (Infrastructure 구현) | 전투 거리 판정 시 엔티티 실제 위치 조회 추상화 |
| `IForfeitService` (Application) | (Infrastructure/Network 구현) | 게임 포기 처리 추상화 |

- **위치 규칙**: 이런 추상화 인터페이스는 `Scripts/Application/Interfaces/`에 둔다.
- **참조 허용 범위**: Application 레이어는 Domain/Application 타입 참조 가능, `Unity.Netcode` 직접 참조 금지(NetworkContext 정적 홀더 사용). 단 `UnityEngine.GameObject` 등 기본 Unity 타입은 `IUnitFactory`처럼 필요 시 허용.
- **`IUnitFactory` 멤버**: `GetUnitObject(int)` / `RegisterUnitObject(int, GameObject)` / `InitializeUnitView(UnitData)` — Infrastructure/Network 계층(`NetworkUnit`, `NetworkProductionController`, `NetworkCombatController`, `NetworkUnitMovementController`)이 `IGameServices.GetUnitFactory()`를 통해 `IUnitFactory` 타입으로 접근한다.

### ⚠️ 네임스페이스 이름 충돌 — `Hexiege.Application` 과 `UnityEngine.Application`

이 프로젝트의 Application 레이어 네임스페이스 `Hexiege.Application` 은 **유니티의 `UnityEngine.Application` 클래스와 이름이 겹친다.**

- `Hexiege.*` 안에 있는 파일에서는 바깥 네임스페이스가 `Hexiege` 이므로, 그냥 `Application` 이라고 쓰면 **네임스페이스 `Hexiege.Application` 으로 해석된다.** 그래서 `Application.dataPath` 같은 코드는 **컴파일되지 않는다.**
- **해결: 유니티 쪽을 쓸 때는 반드시 `UnityEngine.Application` 으로 완전 수식한다.** 이미 그렇게 쓰고 있는 코드가 있으니 그 형태를 따르면 된다(예: `Assets/_Project/Scripts/Infrastructure/Debug/FileSink.cs` — 이 함정과 해법이 파일 헤더 주석에 적혀 있다).
- **실측 규모(2026-08-31, `grep` 기준)**: `namespace Hexiege.Application…` 선언 파일 **58개** · `using Hexiege.Application` 계열 지시문 **77곳**(그중 `using Hexiege.Application;` 74곳) · `UnityEngine.Application.` 완전 수식 **24곳 / 9개 파일**.

**개명하지 않기로 한다 (2026-08-31 결정).** 이유는 두 가지다 — ① 완전 수식으로 회피되고 있어 **현재 정상 동작 중**이고, ② 개명은 위 135곳 이상(선언 58 + 사용 77)을 건드리는 **코드 리팩터**라 문서 정리와는 성격이 다르다. **후속 판단 대기 항목으로 남긴다** — 레이어 이름을 손보는 별도 작업이 생기면 그때 함께 검토한다.

> 🔴 이것은 **회전 연산의 이름을 `RotateCoord`/`RotateState` 로 나눈 것과 같은 부류의 문제**다(「`SymmetricMapBuilder` 생성 경계」 절 — 종전에는 좌표 변환과 상태 변환이 한 이름이었다). **같은 이름이 서로 다른 것을 가리켜, 컴파일러는 구분해도 사람은 구분하지 못한다.** 다른 점은 그쪽은 문서상의 이름이라 고쳤고 이쪽은 코드 전반의 이름이라 미뤘다는 것뿐이므로, **새로 이름을 짓는 자리에서는 이 부류를 만들지 않는다.**

### ViewConverter 시스템 (Core 레이어)

멀티플레이 팀별 관점 처리를 위한 좌표 변환 시스템.

- **위치**: `Scripts/Core/ViewConverter.cs`
- **역할**: 서버/도메인 좌표계(Blue 기준 단일)를 Red 클라이언트 뷰 좌표로 반전
- **반전 공식**: `Flip(pos) = 2 * mapCenter - pos` (맵 중심 기준 180° 반전)
- **제공 API**: `IsFlipped`, `ToView()`, `FromView()`, `FlipDirection()`
- **특징**: 스프라이트/메시 자체는 뒤집히지 않음 — 위치(Position)만 반전
- **입력 역변환**: `ScreenToWorldPoint` 결과도 `FromView()`로 역변환 필요
- **방향 반전**: 유닛 FacingDirection도 Red팀에서 FlipDirection() 적용 (NE↔SW, E↔W, SE↔NW)

---

<a id="네트워크-설계"></a>

## 🌐 네트워크 설계

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

### 네트워크 동작 규정은 별도 문서로 이관됐다 — 최상위 원칙 · 이탈 시 승패 기록 · 인게임 이탈 처리 (2026-10-12)

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

### Netcode for GameObjects

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

#### 아키텍처

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

#### 동기화 전략

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

#### 동기화 대상

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

#### 무작위 맵 시작 동기화 (확정 설계, 1~3단계 구현 · 잔여 있음)

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### `MapDefinition` 정규 데이터 계약

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### 생성·검증 실행 모델

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### 결정적 PRNG 및 독립 스트림 계약

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### `SymmetricMapBuilder` 생성 경계

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### archetype generator 알고리즘

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### 중립 광산 canonical orbit sampling

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### `MapDefinitionValidator` access metric

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### 필수 통로 validator

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### deterministic fallback 정의

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### `HexTile` 런타임 상태 계약

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

##### 재경기 `MapDefinition` 생명주기

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

<a id="결과-화면-이탈-판정통보-구조-2026-09-16-확정-설계--미구현"></a>

#### 결과 화면 이탈 판정·통보 구조 (단계 6 구현 · 관측 범위별 검증)

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.

#### 치팅 방어

기술 원본: [TechnicalDesignDocument_Network.md](TechnicalDesignDocument_Network.md). 이 제목은 기존 목차·코드 주석·작업 문서 참조를 위한 안내다.
<a id="백엔드-설계"></a>

## 🗄️ 백엔드 설계

### Firebase 구조

```
Unity 클라이언트
    ↓
Firebase SDK v13.11.0 + GPGS v2.1.0
    ↓
Firebase Services
    ├─ Firebase Authentication  (로그인 — 익명 / Google Play Games / 이메일+비밀번호)
    ├─ Google Play Games Plugin (Google 로그인 OAuth 브릿지)
    ├─ Firestore               (유저 데이터 / 실시간 리더보드)
    ├─ Firebase Functions      (경기 결과 처리, IAP 영수증 검증)
    └─ Google Play Billing     (인앱 결제 — 스킨/배틀패스)
```

### UGS 연결 구조 (인게임 멀티플레이)

**실계정 (Google / 이메일)**
```
Firebase Auth (로그인) → Firebase ID Token 발급
                                ↓
        LoginUseCase.BridgeToUGSAsync()
                                ↓ SignInWithOpenIdConnectAsync("oidc-firebase", token)
        Unity Gaming Services (PlayerId 계정 귀속)
                                ↓
        UGS Lobby + Relay + Cloud Save + Leaderboard + Economy
                                ↓
        NGO 멀티플레이 세션
```

**익명 계정**
```
Firebase Auth (익명 로그인)
        ↓
LoginUseCase.BridgeToUGSAsync()
        ↓ SignInAnonymouslyAsync (기기 종속 PlayerId)
Unity Gaming Services (Lobby + Relay)
        ↓
NGO 멀티플레이 세션
```

### 로그인 흐름 (AuthSystemRules.md 참조)

| 방식 | 구현 파일 | 상태 |
|------|----------|------|
| 익명 로그인 | `FirebaseAuthService.cs` | ✅ 코드 완료 |
| Google Play Games | `FirebaseAuthService.cs` | ✅ 코드 완료 (GPGS 클라이언트 ID 미설정) |
| 이메일+비밀번호 | `FirebaseAuthService.cs` | ✅ 코드 완료 (Firebase Console 설정 미완료) |
| 계정 연동 (익명→실계정) | `AccountLinkUseCase.cs` | ✅ 코드 완료 |

### Firebase Functions 예정 기능

| 함수 | 역할 |
|------|------|
| `completeMatch` | 경기 결과 처리 + Firestore 랭킹 갱신 |
| `verifyPurchase` | Google Play Billing IAP 영수증 검증 |

---

<a id="데이터베이스-스키마"></a>

## 💾 데이터베이스 스키마

### Firestore 컬렉션 구조

#### users/{firebaseUid}
```json
{
  "displayName": "한용희",
  "stats": {
    "totalGames": 120,
    "wins": 65,
    "losses": 55,
    "rankPoints": 1450
  },
  "inventory": {
    "races": ["human", "spirit"],
    "skins": []
  }
}
```

#### matches/{matchId}
```json
{
  "mode": "custom",
  "duration": 635,
  "players": {
    "blue": { "userId": "firebase_uid_A", "race": "human" },
    "red": { "userId": "firebase_uid_B", "race": "spirit" }
  },
  "result": {
    "winner": "blue",
    "blueStats": { "tilesControlled": 48, "unitsKilled": 35 },
    "redStats": { "tilesControlled": 32, "unitsKilled": 28 }
  },
  "timestamp": "2026-06-05T12:00:00Z"
}
```

#### leaderboard/{rankId}
```json
{
  "userId": "firebase_uid_A",
  "displayName": "한용희",
  "rankPoints": 1450,
  "wins": 65,
  "updatedAt": "2026-06-05T12:00:00Z"
}
```

---

<a id="디자인-패턴"></a>

## 🎨 디자인 패턴

### 1. Singleton Pattern
```csharp
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }
    
    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

### 2. Object Pool Pattern
```csharp
public class ObjectPool<T> where T : Component {
    Queue<T> pool = new Queue<T>();
    T prefab;
    
    public T Get() {
        if (pool.Count > 0) return pool.Dequeue();
        return Object.Instantiate(prefab);
    }
    
    public void Return(T obj) {
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

### 3. Command Pattern
```csharp
public interface ICommand {
    void Execute();
    void Undo();
}

public class SpawnUnitCommand : ICommand {
    Unit unit;
    HexCoord position;
    
    public void Execute() {
        UnitFactory.Create(unit, position);
    }
    
    public void Undo() {
        UnitFactory.Destroy(unit);
    }
}
```

### 4. State Pattern
```csharp
public interface IUnitState {
    void Enter(Unit unit);
    void Update(Unit unit);
    void Exit(Unit unit);
}

public class IdleState : IUnitState {
    public void Enter(Unit unit) { unit.StopMoving(); }
    public void Update(Unit unit) {
        if (unit.FindEnemy() != null) {
            unit.ChangeState(new AttackState());
        }
    }
    public void Exit(Unit unit) { }
}
```

### 5. Observer Pattern (UniRx)
```csharp
// 이벤트 발행
GameEvents.OnUnitSpawned.OnNext(new UnitSpawnedEvent(unit));

// 이벤트 구독
GameEvents.OnUnitSpawned
    .Subscribe(e => Debug.Log($"Unit spawned: {e.Unit.Type}"))
    .AddTo(this);
```

### 6. Factory Pattern
```csharp
public class UnitFactory {
    Dictionary<UnitType, GameObject> prefabs;
    
    public Unit Create(UnitType type, Vector3 position) {
        var prefab = prefabs[type];
        var unit = Object.Instantiate(prefab, position, Quaternion.identity);
        return unit.GetComponent<Unit>();
    }
}
```

### 7. Strategy Pattern
```csharp
public interface IRaceStrategy {
    void ApplyBonus(Unit unit);
}

public class HumanRaceStrategy : IRaceStrategy {
    public void ApplyBonus(Unit unit) {
        unit.Stats.AttackDamage *= 1.1f;
    }
}
```

### 8. UI DOTween 애니메이션 패턴 (2026-03-19)

인게임 UI 패널의 등장/퇴장 애니메이션을 일관되게 처리하기 위한 프레임워크.

#### UIAnimator (static 헬퍼)

`Assets/_Project/Scripts/Presentation/UI/Common/UIAnimator.cs` — DOTween Sequence를 반환하는 정적 메서드 모음.

```csharp
// 사용 가능한 메서드
UIAnimator.PopupShow(CanvasGroup cg, Transform tf, float duration);
UIAnimator.PopupHide(CanvasGroup cg, Transform tf, Action onComplete, float duration);
UIAnimator.SlideInFromBottom(RectTransform rt, CanvasGroup cg, float offset, float duration);
UIAnimator.SlideOutToBottom(RectTransform rt, CanvasGroup cg, Action onComplete, float offset, float duration);
UIAnimator.SlideInFromTop(RectTransform rt, CanvasGroup cg, float offset, float duration);
UIAnimator.SlideOutToTop(RectTransform rt, CanvasGroup cg, Action onComplete, float offset, float duration);
```

#### UIManager — 전역 공통 UI 싱글톤

`Assets/_Project/Scripts/Presentation/UI/UIManager.cs` — Login 씬에서 1회 생성, DontDestroyOnLoad로 모든 씬에서 유지되는 공통 UI 매니저.

```
[UI Systems] (Login.unity)
└─ UIManager (SingletonMonoBehaviour<UIManager> + IUIManager)
    └─ UIManager Canvas (SortingOrder 100)
        └─ SafeAreaContainer (SafeAreaFitter)
            ├─ ConfirmPopup     ← _confirmPopup 연결
            └─ LoadingIndicator ← _loadingIndicator(CanvasGroup) 연결

SplashOverlay Canvas (씬 루트, SortingOrder 200)
└─ SplashOverlay (CanvasGroup + SplashOverlayView)
    ├─ Background (SafeArea 밖 — 전체화면)
    └─ SafeAreaContainer
        ├─ StatusText ("로딩 중...")
        └─ TapToStartText ("Tap to Start")

Toast (씬 루트) ← ToastUI 프리팹, 자체 DontDestroyOnLoad
```

**외부 호출 패턴** (null-safe — Login 씬 직접 진입 시 Instance=null 가능):
```csharp
UIManager.Instance?.ShowConfirm("메시지", onConfirm, onCancel);
UIManager.Instance?.ShowLoading(true, "로딩 중...");
UIManager.Instance?.ShowLoading(false);
```

**LoadingIndicator 설계 원칙**: 로딩 사유(씬 전환/Firebase/매칭 등)와 로딩 UI를 분리. 모든 로딩 상황에 `ShowLoading(bool, string)` 단일 API 사용.

---

#### AnimatedPanel 컴포넌트

`Assets/_Project/Scripts/Presentation/UI/Common/AnimatedPanel.cs` — 패널 GameObject에 부착하여 Show()/Hide() 호출만으로 애니메이션 자동 처리.

```csharp
// Inspector 설정
AnimationType: PopupFade | SlideFromBottom | SlideFromTop
ShowDuration: 0.25f (인게임 컨텍스트/게임 상태 패널 기준)
HideDuration: 0.2f
SlideOffset: 300f (px)
BackgroundOverlay: CanvasGroup (선택적 — 슬라이드 패널 배경용)

// 코드에서 호출
_panel.Show();
_panel.Hide(onComplete: () => { /* 퇴장 완료 후 실행 */ });
```

**배경 오버레이 규칙**: `_backgroundOverlay`(CanvasGroup 타입)가 연결된 경우 Show() 시 즉시 `alpha=1 / blocksRaycasts=true / interactable=true`, Hide() 완료 후 즉시 `alpha=0 / false / false`. 패널 슬라이드 애니메이션과 독립적으로 즉시 처리. 배경 GameObject는 항상 active 상태 유지.

**분류별 애니메이션 기준** → `Assets/_Project/Docs/UIGuidelines.md` 참조.

---

### 9. UI 팝업 구현 패턴

팝업 UI 구현 시 배경 클릭으로 창을 닫는 기능을 구현할 때 발생하는 문제를 방지하기 위해 다음 패턴을 권장합니다.

#### 문제 상황

- 팝업 패널(`BuildingPanel`)이 콘텐츠 영역보다 큰 투명한 배경을 가질 경우, 이 투명한 영역이 화면 전체를 덮는 닫기 버튼(`Background`)으로의 클릭을 가로막습니다.
- 패널의 `Raycast Target`을 끄면 패널 내부의 버튼까지 클릭이 통과해버리는 문제가 발생합니다.

#### 해결 구조

역할에 따라 게임 오브젝트를 명확히 분리합니다.

```
PopupCanvas
  ├─ Background (전체 화면, Raycast Target ON, 팝업 닫기 Button 컴포넌트)
  └─ BuildingPanel (레이아웃 그룹 역할, Image 컴포넌트 없음, Raycast Target 없음)
      ├─ PanelVisuals (실제 패널 배경 이미지, Raycast Target OFF, 순수 시각적 요소)
      └─ Buttons (버튼들, Raycast Target ON, 실제 상호작용 요소)
```

#### 핵심 원리

1.  **클릭 통과용 시각 요소**: `PanelVisuals`는 `Raycast Target`을 꺼서 시각적으로만 존재하고 모든 클릭을 통과시킵니다.
2.  **클릭 가로채기용 상호작용 요소**: `Buttons`는 `Raycast Target`을 켜서 클릭을 받고 자신의 기능을 수행합니다.
3.  **최후의 클릭 수신자**: 패널의 빈 공간이나 버튼이 아닌 곳을 클릭하면, 모든 클릭은 최하단에 깔린 `Background`에 도달하여 팝업을 닫는 `onClick` 이벤트를 실행합니다.

이 구조는 UI의 시각적 표현과 상호작용 로직을 분리하여 예측 가능하고 안정적인 동작을 보장합니다.

<a id="9-popupclosedframe-팝업-닫힘-프레임-보호"></a>

### 10. PopupClosedFrame (팝업 닫힘 프레임 보호)

팝업이 닫힌 직후 같은 프레임에서 배경 클릭이 통과하는 문제를 방지하는 패턴.

#### 문제 상황

- 팝업 Background 버튼 클릭 → `Close()` 호출 → 같은 프레임에서 `InputHandler.HandleClick`이 실행
- 결과: 팝업 뒤의 타일이 의도치 않게 클릭됨

#### 해결 방법

```csharp
// BuildingPlacementUI / ProductionPanelUI
public static int ClosedFrame { get; private set; } = -1;

void Close() {
    ClosedFrame = Time.frameCount;
    gameObject.SetActive(false);
}
```

```csharp
// InputHandler에서 체크
if (Time.frameCount == BuildingPlacementUI.ClosedFrame) return;
if (Time.frameCount == ProductionPanelUI.ClosedFrame) return;
```

각 팝업 UI가 `ClosedFrame`에 닫힌 프레임 번호를 기록하고, `InputHandler`가 같은 프레임의 클릭을 무시합니다.

---

<a id="육각형-그리드-시스템"></a>

## 🔷 육각형 그리드 시스템

### Cube Coordinates
```
육각형 좌표계:
    q (column)
    r (row)
    s = -q - r

   (-1,1)  (0,1)
      \    /
  (-1,0) (0,0) (1,0)
      /    \
   (0,-1) (1,-1)
```

### 듀얼 Orientation 지원

두 가지 타일 방향을 런타임에서 전환 가능:

| 항목 | PointyTop | FlatTop |
|------|-----------|---------|
| 타일 모양 | 꼭지점 12시 | 변 12시 |
| 그리드 크기 | 7×17 | 10×29 |
| TileWidth | 0.866 | 1.0 |
| TileHeight | 0.866 | 0.866 |
| Offset 방식 | even-r (홀수 행 시프트) | even-q (홀수 열 시프트) |
| 아트 방향 수 | 3 (NE, E, SE) | 4 (N, NE, SE, S) |

```csharp
// OrientationConfig: Orientation별 그리드 설정
[System.Serializable]
public class OrientationConfig {
    public int GridWidth;
    public int GridHeight;
    public float TileWidth;
    public float TileHeight;
}

// GameConfig에서 PointyTop/FlatTop 인스턴스로 관리
public OrientationConfig PointyTop = new OrientationConfig { ... };
public OrientationConfig FlatTop = new OrientationConfig { ... };

// 런타임 맵 전환
public void LoadMap(HexOrientation orientation) {
    OrientationConfig oc = (orientation == HexOrientation.FlatTop)
        ? _config.FlatTop : _config.PointyTop;
    // 설정 적용 → 그리드 생성 → UseCase → 렌더링 → 카메라 → 유닛
}
```

### HexCoord 구조체
```csharp
public struct HexCoord {
    public int Q, R;
    public int S => -Q - R;

    public static int Distance(HexCoord a, HexCoord b) {
        return (Mathf.Abs(a.Q - b.Q) + Mathf.Abs(a.R - b.R) + Mathf.Abs(a.S - b.S)) / 2;
    }
}
```

### A* 경로찾기 (커스텀 구현)
```csharp
// HexPathfinder: 커스텀 A* 경로탐색
// 헥스 그리드 특화, 6방향 이웃 탐색, 이동 불가 타일 우회
// blockedCoords: 적 유닛 좌표 등 추가로 이동 불가 처리할 좌표 집합
List<HexCoord> path = HexPathfinder.FindPath(grid, start, goal, blockedCoords);
```

**경로 차단 (blockedCoords)**:
- 모든 다른 유닛(아군/적군 무관)의 현재 Position을 이동 불가로 처리
- **같은 팀** 유닛의 ClaimedTile(이동 중 선점 타일)도 차단 목록에 포함 → 아군끼리 겹침 방지
- **적 팀**의 ClaimedTile은 차단하지 않음 → 적과의 타일 경합은 전투로 해결
- UnitMovementUseCase가 RequestMove() 시 자기 자신을 제외한 모든 살아있는 유닛 좌표 + 같은 팀 ClaimedTile을 HashSet으로 구성하여 전달
- **목표 타일은 차단 체크 제외** (2026-03-18 수정): 경로 중간 타일에만 blocked 적용. 목표 타일도 blocked 체크 시, 인접 타일이 모두 선점되면 Castle에 도달 불가한 교착 상태 발생 가능

**ClaimedTile (이동 중 타일 선점)**:
- UnitData.ClaimedTile (HexCoord?) — Lerp 시작 전 설정, Lerp 완료 후 해제
- 같은 팀 유닛만 이 타일을 이동 불가로 인식 (경로탐색 시 우회)
- 적 팀에게는 투과 → 같은 타일에 적이 진입 시 전투 발생

**Per-step 타일 가용성 체크 (이동 중 실시간 검증)**:
- MoveAlongPath에서 각 스텝 시작 전 `IsTileBlockedBySameTeam()` 호출
- 같은 팀 유닛의 Position 또는 ClaimedTile이 다음 타일과 겹치면 차단 판정
- 차단 시 현재 위치에서 최종 목적지까지 재탐색 (RequestMove) → 새 경로로 교체
- 재탐색 실패 시 이동 중단 (Idle 복귀)
- 적 팀은 체크하지 않음 — 전투로 해결

**유닛 스폰 검증**:
- UnitSpawnUseCase.SpawnUnit()에서 계산된 타일 `IsWalkable` 검증 + 유닛 점유 검증 (GetUnitAt)
- 건물이 있거나 다른 유닛이 이미 있는 타일에는 유닛 생성 불가

---

<a id="ai-시스템"></a>

## 🤖 AI 시스템

### 유닛 AI 상태머신 (MVP 목표)
```
Idle State
   ↓
 적 발견?
   ↓
Attack State
   ↓
 적 사망?
   ↓
Move State (랠리 포인트)
   ↓
도착
   ↓
Idle State
```

### AI 스크립트 구조 (MVP 목표)
```csharp
public class UnitAI : MonoBehaviour {
    IUnitState currentState;
    Unit unit;

    void Update() {
        currentState?.Update(unit);
    }

    public void ChangeState(IUnitState newState) {
        currentState?.Exit(unit);
        currentState = newState;
        currentState.Enter(unit);
    }
}
```

### 현재 구현: 전투 시스템 (프로토타입)

프로토타입에서는 State 패턴 대신 코루틴 기반으로 이동→공격 흐름 구현.

#### IDamageable 인터페이스

유닛과 건물의 전투 대상을 통합하는 인터페이스:
```csharp
public interface IDamageable {
    int Id { get; }
    TeamId Team { get; }
    HexCoord Position { get; }
    int Hp { get; }
    int MaxHp { get; }
    bool IsAlive { get; }
    void TakeDamage(int damage);
}
```
UnitData와 BuildingData 모두 IDamageable을 구현하여 UnitCombatUseCase가 동일한 로직으로 공격 가능.

### 싱글플레이 AI 오퍼넌트 시스템 (2026-06-10 확정)

싱글플레이 모드에서 Red 팀을 자동으로 운영하는 AI 오퍼넌트. 빌드오더 스크립트와 반응 시스템으로 구성된다.

**아키텍처 (레이어 배치):**
- `DifficultyLevel`, `AIActionType`, `BuildOrderStep` → `Hexiege.Domain` (`Scripts/Domain/AI/`)
- `AIScenarioConfig` ScriptableObject → `Hexiege.Infrastructure`
- `AIOpponentController` → `Hexiege.Application` (빌드오더 + 반응 시스템)
- `GameBootstrapper` → 시나리오 번들 로드 및 AI 주입 (composition root)

> DifficultyLevel·AIActionType·BuildOrderStep를 Domain 레이어로 분리한 이유: Application 레이어의 AIOpponentController가 이 타입들을 직접 참조하므로, Infrastructure에 두면 Application→Infrastructure 직접 의존이 발생한다. Domain 분리로 의존 방향(Application→Domain)을 정상화했다.

**에셋 구조:**
- 종족별 1개 에셋 × 3개 시나리오
  - `AIScenarioConfig_Human.asset` (Human_A / Human_B / Human_C)
  - `AIScenarioConfig_Spirit.asset` (Inferno / Torrent / Quake)
  - `AIScenarioConfig_Transcendence.asset` (Rush / Flora / Beast)
- 게임 시작 시 `GameRaceContext.RedRace`로 종족을 판별한 뒤 해당 에셋에서 무작위 시나리오 1개를 선택한다.

**ScriptableObject 경로:** `Resources/Config/AIScenarioConfig_{종족}.asset`

#### 중앙 집중 스탯 관리 — ScriptableObject 기반 (2026-04-25 전환)

타입별 기본 스탯을 ScriptableObject로 관리. Inspector에서 코드 수정 없이 수치 편집 가능.

```csharp
// UnitStats: UnitStatsConfig ScriptableObject 기반
// 에셋: Assets/_Project/Resources/Config/UnitStatsConfig.asset
// 내부: Dictionary<UnitType, StatValues> O(1) 조회
// API: UnitStats.GetMaxHp(type), GetAttackPower(type), GetAttackRange(type),
//      GetMoveSeconds(type), GetAttackCooldown(type)
// 유닛 9종 (Pistoleer=0 ~ LionKnight=8) + 신규 16종 추가 예정
// 사거리 임계값(world units) = AttackRange * TileHeight(0.866) + 0.05f (Epsilon)

// BuildingStats: BuildingStatsConfig ScriptableObject 기반
// 에셋: Assets/_Project/Resources/Config/BuildingStatsConfig.asset
// 내부: Dictionary<(BuildingType, RaceId), StatValues>
// BuildingType 26종 × 3종족(Human/Spirit/Transcendence) 조합
// API: BuildingStats.GetMaxHp(type, race), GetGoldCost(type, race),
//      BuildingStats.GetAttackCooldown(type, race), GetUpgradeCost(type, race),
//      BuildingStats.GetTotalInvestedCost(type, race) — 누적 투자비 캐시
```

#### 전투 스탯

**유닛 (UnitData)**:
```csharp
public class UnitData : IDamageable {
    public int MaxHp { get; }          // UnitStats에서 결정
    public int Hp { get; private set; }
    public int AttackPower { get; }    // UnitStats에서 결정
    public int AttackRange { get; }    // UnitStats에서 결정
    public bool IsAlive => Hp > 0;
    public HexCoord? ClaimedTile { get; set; } // 이동 중 선점 타일 (같은 팀만 차단)
}
```

**건물 (BuildingData)**:
```csharp
public class BuildingData : IDamageable {
    public int MaxHp { get; }          // BuildingStats에서 결정
    public int Hp { get; private set; }
    public bool IsAlive => Hp > 0;
}
```

#### 전투 흐름 (이동 중 거리 기반 전투)
```
유닛 이동 명령 (InputHandler / AutoMove)
  ↓
A* 경로 계산 (아군/적군 Position 우회 + 같은 팀 ClaimedTile 우회)
  ↓
각 스텝마다:
  ↓
다음 타일 가용성 체크 (IsTileBlockedBySameTeam)
  ↓ 차단됨
현재 위치 → 최종 목적지 재탐색 (RequestMove) → 새 경로로 교체
  ↓ 통과
ClaimedTile = 다음 타일 (같은 팀 겹침 방지)
  ↓
타일→타일 Lerp 이동 (UnitView 코루틴)
  ↓ Lerp 중 매 프레임
사거리 내 적(유닛/건물) 탐색 (UnitCombatUseCase.TryAttack)
  ↓ 적 발견
이동 중단 → 공격 방향 계산 → IDamageable.TakeDamage() → 이벤트 발행
  ↓
적 HP ≤ 0? → EntityDied 이벤트 → View 파괴 + Dictionary 제거
  ↓
사거리 내 적이 남아있으면 반복 공격
  ↓
전투 승리 → 남은 Lerp 계속 → 타일 중앙 도착 = 점령
  ↓
ClaimedTile 해제, ProcessStep(Position 갱신 + SetOwner)
  ↓
모든 경로 이동 완료 → Idle 상태 복귀
```

**핵심 규칙: 타일 중앙 도착 = 전투 승리 = 점령**
- 전투는 Lerp 이동 중에 거리 기반으로 발동 (타일 중앙 도착 전)
- 패배한 유닛은 타일 중앙에 도달하지 못하므로 점령 불가
- SetOwner는 Lerp 완료 후 ProcessStep에서만 호출 (변경 없음)

#### Deep Authoritative Locomotion 구현 구조 (2026-08-04 Editor 조건부 PASS)

B3의 신규 서버 writer는 10°/15° fallback 정렬, 이동 금지 틱의 진행량 보존, target-acquire 우선, endpoint `NoIntent`, 경기 단위 mode 고정과 Client Simulation Root write 금지의 정확성 계약을 유지한다. 과거 멀티플레이 증거의 reducer rejection, gate failure, writer 선택 오류, client write, 공격 writer 충돌은 0이었다. 이후 확인된 정상 60° 코너의 stop-turn-go를 교정하기 위해 아래 목표 구조를 실제 런타임에 연결했다. NetworkTransform과 Visual Root는 서버가 commit한 결과를 계속 복제·표현할 뿐 trajectory를 계산하지 않는다.

구현은 다음 두 층으로 구성한다.

1. **Authoritative Locomotion Module / 공통 commit seam**
   - 서버 pose, trajectory 표본, `deltaTime`, command/segment scope를 받아 phase, SimulationFacing, 다음 position/rotation, 진행량과 복제 snapshot을 하나의 commit으로 결정한다.
   - A*, Chase, PendingRepath, PostCombatResume는 이 Module의 Adapter이며 직접 Transform 진행량·회전·phase를 각각 계산하지 않는다.
   - 정렬로 위치가 보류되면 진행 시간·거리도 보류하고, 재개 시 jump 없이 같은 trajectory에서 이어 간다.
   - Legacy와 ReducerAuthoritative가 공존하는 기간에는 두 Adapter가 같은 Seam을 사용하며 경기 단위 mode에 따라 정확히 하나만 활성화한다.

2. **Server Trajectory Planner (`UnitServerTrajectoryPlanner`)**
   - 현재 선분과 다음 선분, 이동 가능 타일 통로, 재경로·추격·전투 복귀 의도를 받아 연속적인 위치 접선과 DesiredMoveDirection을 만든다.
   - 위치 진행 방향과 SimulationFacing은 같은 접선을 사용한다. 이전 직선으로 움직이면서 방향만 다음 선분으로 미리 돌리는 구현은 금지한다.
   - 완만한 코너는 10°/15° 계약 안에서 `Move`를 유지하고, 새 명령·급격한 재경로 등 연속 전환이 안전하지 않을 때만 `AlignToMove`를 사용한다.
   - trajectory는 이동 가능 타일 통로를 벗어나거나 차단 타일을 가로지르지 않으며 기존 타일 도착 순서·점령·목적지 의미를 보존한다.

**[구현 완료/실기 전체 검증 대기]** `UnitTrajectoryStep`은 candidate position·facing·진행량·hold/waypoint 상태를 한 결과로 전달하고, `UnitPathCheckpointTracker`는 곡선이 waypoint 중심을 정확히 밟지 않아도 실제 trajectory의 통과를 순서대로 한 번만 소비한다. Planner는 최초 Simulation Root point와 A* logical path를 입력으로 logical path corridor를 만들며 경유 타일 중심 통과를 강제하지 않는다. 1차 안전 판정은 Simulation Root point sweep이고, 실패하면 안전한 후보 또는 재경로로 닫는다. `MoveSpeed`는 실제 trajectory 거리/초로 적분한다. 위치 진행 방향과 `SimulationFacing`은 같은 접선에서 계산해 오차를 1° 이내로 제한한다. 공통 최대 회전 속도는 270°/s이며, target acquire는 해당 틱의 candidate position으로 판정한다. 획득이 확정된 틱에는 candidate pose까지만 commit하고 `NoIntent`를 게시하며 다음 틱의 추가 이동을 금지한다. 150° 이상의 반전은 정지 정렬 fallback이다.

**검증 Seam:** 순수 trajectory/step 검증은 60° 코너, 180° 재경로, 이동 타겟 추격, 전투 종료 복귀, held tick 진행량 0, 재개 jump 0, velocity↔SimulationFacing 허용 오차, 차단 타일 통과 0과 결정적 동일 입력/동일 결과를 포함한다. 런타임 검증은 Android와 Unity Editor counterpart 역할교대로 Host/Client Simulation Root 수렴, Client reducer/write 0, 25종 공통 경로, 공격·피해·RPC·VFX Legacy 회귀 0을 확인한다. Visual Root smoothing만으로 canonical stop-turn-go를 숨긴 결과는 PASS로 인정하지 않는다.

#### 사망 처리 (Dead Entity Cleanup, 2026-05-18 강타입 분리 / 2026-06-08 NGO Despawn 패턴 수정)
```
UnitCombatUseCase.ExecuteAttack()
  ↓ target.IsAlive == false
GameEvents.OnUnitDied (유닛) 또는 OnBuildingDied (건물) 강타입 이벤트 발행
  ↓
[유닛 사망 — 싱글플레이]
1. UnitView 구독(Presentation) → EffectManager.PlayUnitDeath() → Destroy(gameObject)
2. UnitSpawnUseCase.RemoveUnit() → Dictionary에서 제거

[유닛 사망 — 멀티플레이]
※ 레이어 규칙: Presentation(UnitView)은 Unity.Netcode 직접 참조 금지 → NetworkContext 홀더만 사용
1. 서버: NetworkCombatController(Infrastructure) 구독
   → EntityDiedClientRpc 발행 → NetworkObject.Despawn(destroy:true)
   → 모든 클라이언트에 GO 파괴 전파 (Destroy(gameObject)는 NGO 전파 불보장)
   → 서버측 UnitView도 OnUnitDied 수신 → EffectManager.PlayUnitDeath() (서버 화면 이펙트)
2. 클라이언트: UnitView.OnUnitDied() → NetworkContext.IsNetworkActive && !IsNetworkServer → return (처리 없음)
   클라이언트: NetworkUnit.OnNetworkDespawn() 자동 호출 → EffectManager.PlayUnitDeath() → GO NGO 파괴
3. 서버/클라이언트 공통: EntityDiedClientRpc 수신 측에서 OnUnitDied 재발행 → UnitSpawnUseCase.RemoveUnit()

[건물 사망/철거]
1. BuildingFactory가 OnBuildingDied 구독 → _buildingObjects Dict O(1) 조회 → GO Destroy
   (BuildingView.cs 삭제됨 — BuildingFactory가 GO 파괴 책임 인수)
2. BuildingPlacementUseCase.RemoveBuilding() → Dictionary 제거 + 타일 `HasBuilding=false` (계산된 `IsWalkable` 자동 갱신, 영구 지형/광산 상태 유지)
```

#### 타일 선택 하이라이트 처리

```csharp
// HexTileView의 OnTileSelected 이벤트 핸들러
// Coord == PreviousCoord일 때 = 선택 해제 이벤트 (Deselect)
// Coord != PreviousCoord일 때 = 새 타일 선택
if (e.Coord == _coord)
{
    _isSelected = !(e.PreviousCoord.HasValue
                    && e.PreviousCoord.Value == e.Coord);
    UpdateColor();
}
```

> **버그 수정 이력:** 초기 구현에서 `_isSelected = !_isSelected` (토글)을 사용했으나,
> Deselect() 이벤트(Coord == PreviousCoord)에서 Check1(해제)과 Check2(토글)가 동일 타일에서
> 연속 실행되어 하이라이트가 잔존하는 버그 발생. 결정적(deterministic) 할당으로 수정.

#### 이벤트 기반 전투 통신 (2026-05-18 강타입 이벤트로 분리)

유닛/건물 사망 이벤트를 강타입으로 분리하여 구독자의 is-캐스팅 필터 전면 제거:

```csharp
// 공격 이벤트 (UnitCombatUseCase → UnitView)
GameEvents.OnEntityAttacked.OnNext(new EntityAttackedEvent(attacker, target));

// 유닛 사망 이벤트 (UnitCombatUseCase → UnitView, UnitSpawnUseCase 등)
GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unitData));

// 건물 사망/철거 이벤트 (BuildingPlacementUseCase → BuildingFactory 등)
GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(buildingData));
```

**이벤트 매칭**: View에서 자신의 엔티티를 식별할 때 **Id 비교** 사용:
```csharp
// UnitView에서
if (e.Unit.Id == _unitData.Id) { /* 이 유닛이 사망 */ }
// BuildingFactory에서
if (_buildingObjects.TryGetValue(e.Building.Id, out var go)) { Destroy(go); }
```

#### 서버 권위 Unit ActionSequence 목표 구조 (2026-08-18 B3 Android 진행성 교정 중)

**현재 구현 상태:** A1의 `UnitActionContracts`·`UnitActionSequencer`, A2 서버 pose seam, B1 Simulation/Visual Root seam과 B2 pure `UnitMovementReducer` Shadow를 거쳐 B3 경기 단위 `ReducerAuthoritative` single-writer와 이동 phase 복제를 연결했다. `UnitServerTrajectoryPlanner`와 공통 commit adapter가 A*·Chase·PendingRepath·PostCombatResume의 연속 이동, 실제 거리 기반 waypoint 소비, candidate-position target acquire와 committed `SimulationFacing → UnitData.Facing` 동기화를 담당한다. 후속 실기에서 확인한 동일 path·stale PendingPath 과잉 종료는 코루틴보다 오래 사는 `UnitNavigationObjective`의 `Navigating/WaitingRepath/Blocked/Completed/Cancelled` lifecycle로 교체했다. 동일 path와 A→B→A는 frame당 1회·무진전 8회 상한 안에서 재평가하고 실제 진전 또는 walkability revision에서 이력을 초기화한다. 현재 위치를 포함하지 않는 PendingPath는 서버 현재 위치에서 다시 요청하며, 막힌 목표는 코루틴과 목표를 유지한다. `ProductionTicker`는 활성 목표에 같은 공성 command를 재발행하지 않는다. 위치 변화가 없는 대기/막힘은 `UnitAnimState.Held`를 서버 값으로 복제하고 Walk 첫 pose를 고정하며 `Frozen`과 우선순위를 합성한다. endpoint가 이미 `NoIntent`인 경우 cleanup은 같은 snapshot을 중복 게시하지 않는다. Unity C# 런타임·Editor 컴파일은 PASS했지만 Editor self-validation과 Android Host 실기 증거 전이므로 B3는 계속 **FAIL / OPEN**이다. 공격 result seam과 피해·RPC·VFX 권위 전환도 미완료다.

##### Legacy 피격 표현 큐 (Hit Presentation Queue, 2026-07-12 전투 타격 타이밍 동기화)

데이터(HP)는 서버 시계를, 연출(HP 텍스트·피격 VFX·타격 반응)은 각 클라이언트의 로컬 타격 프레임을 따르도록 역할을 분리한다.

- `EntityDamagedEvent` / `SyncHealthClientRpc`에 **공격자 정보(Id + 유닛 여부)** 를 추가했다. 도메인 HP는 서버 값 도착 즉시 갱신(서버 권위 유지)하되, 연출은 보류한다.
- `HitPresentationQueue`(Presentation 신규)가 피격 정보를 공격자별로 큐에 보류하고, 공격자의 로컬 `UnitView.OnAttackHit` 시점에 FIFO 방출한다. 타임아웃(쿨다운×1.5)·타겟 사망·공격자 사망·공격자 전투 중단 시 즉시 방출한다.
- `HitFrameTimes`(데미지 타격 시점)는 Attack 클립 `OnAttackHit` Animation Event 시간에서 `UnitFactory`가 자동 추출한다(수동 입력은 폴백). 데미지는 항상 서버 타이머로 적용하며 Animator 상태에 종속시키지 않는다.
- 연출 API: `EffectManager.PlayUnitHit`(+`UnitEffectConfig.hitPreset`), `PlayBuildingAttack`(타워 발사, +`BuildingEffectConfig.attackPreset`), `TracerProjectile`(원거리 트레이서, +`tracerPreset`). HP 텍스트는 `FloatingHpTextSpawner.ShowDamage` 단일 진입점으로 통합.
- 상세 규칙: `GameSystemRules_Units.md` 규칙 17~21, `GameSystemRules_Buildings.md` 방어 타워 시스템 규칙 12(타워 발사 연출).
- 구 `UnitEffectView.cs`(DEPRECATED)는 이 파이프라인이 역할을 대체하여 삭제됨.

#### 유닛 애니메이션 상태 동기화 (2026-07-13 엣지 RPC → NetworkVariable 레벨 동기화)

멀티플레이에서 유닛 애니메이션 상태(Walk / Attack)의 동기화 방식을 **"상태가 바뀌는 순간에만 1회성으로 쏘는 엣지 트리거 RPC"** 에서 **"현재 상태 값 자체를 공유하는 레벨 동기화(NetworkVariable)"** 로 전환했다.

- **전(엣지 RPC):** `StartWalkAnimationClientRpc` / `GameEvents.OnUnitWalkStarted` 등 1회성 신호로 클라이언트에 상태 변화를 통지. 신규 유닛이 구독을 완료하기 전에 첫 신호가 도착하면 유실되고 이후 바로잡을 수단이 없었다(스폰 레이스 — 클라 스폰 223기 중 222기에서 첫 Walk RPC 유실 실측). 갓 생산된 유닛에서 걷기 모션이 안 나오는 원인.
- **후(레벨 동기화):** `NetworkUnit`에 `UnitAnimState`(None / Walk / Attack) `NetworkVariable`(ReadPermission=Everyone / WritePermission=Server, `_unitId` 패턴 재사용)을 두고 서버가 상태 진입 지점에서 값을 쓴다. 클라이언트는 `OnValueChanged` + **스폰 시 현재 값 자동 적용**으로 항상 서버의 현재 상태를 받으므로 신호 유실이 구조적으로 불가능하다. 호스트/싱글플레이는 기존 로컬 Animator 직접 제어를 유지.
- **적용 시점 봉합:** 애니메이션 적용이 `UnitView.Initialize`(애니메이터 준비)보다 이르면 무음 실패하므로, `UnitView.Initialize` 말미에서 `NetworkUnit.ReapplyAnimStateToView()`로 현재 값을 **멱등 재적용**한다.
- **역할 분리 유지:** 데미지 판정은 서버 타이머(규칙 18), 조준 회전은 별도 타겟 참조(규칙 12·15)로 애니메이션 상태 값과 분리. `_combatAnimationSent`는 애니메이션이 아니라 데미지(`ExecuteAttack`)·타겟 RPC 게이팅 가드로 유지.
- **부수 위치 보정:** 재경로 재발급 시 첫 스텝이 최종 목적지 역방향으로 향하던 "뒤로 밀림"(서버 경로 자체가 원인, 클라 보간 무죄)은 `MoveTo`의 `AlignPathStartToTransform`로 실제 `transform` 전방 타일(`FindForwardClosestTile`)에서 재발급하여 보정(규칙 11 강제).
- 상세 규칙: `GameSystemRules_Units.md` 규칙 22(규칙 21을 상위 대체). task: `_Tasks/2026-07-12/07_55_movement-walk-anim-sync/`.

#### 특수 공격 전략 핸들러 구조 (2026-07-17 도끼병 휩쓸기형 AoE)

일반 유닛의 단일 타깃 피해와 별개로, 특수 능력(휩쓸기 / 착탄 / 파도 / DoT / 힐)을 가진 유닛의 추가 피해·효과를 **전략(핸들러) 패턴**으로 분리했다. 특수 유닛 5종(BattleAxe / QuakeSpirit / TorrentSpirit / MushroomBomber / BloomFairy) 중 도끼병이 첫 구현.

- **계약/구성:** `ISpecialAttackBehavior.Apply(SpecialAttackContext)` 인터페이스 + `SpecialAttackContext`(공격자·주 타깃·유닛 목록·재사용 피해 헬퍼·월드 좌표 조회 수단·reach/arc) + `SpecialAttackRegistry`(`UnitType → 핸들러` 매핑, 현재 `BattleAxe → SweepAttackBehavior`만) + 유닛별 핸들러. 모두 `Scripts/Application/Combat/`. `UnitType` 키 매핑이라 인스펙터 배선 불필요.
- **피해 수렴점 단일화:** `UnitCombatUseCase.ExecuteAttack`의 인라인 단일 피해 로직을 `ApplyDamageToVictim` 헬퍼로 추출하여 주 타깃과 AoE 대상이 **같은 피해·이벤트·사망 처리 경로**를 쓰게 했다(멀티플레이 HP 동기화 일관). `ExecuteAttack` 말미에 특수 공격 훅 1줄만 추가 → 신규 특수 유닛은 핸들러 + 레지스트리 1줄로 확장하며 `ExecuteAttack` 재수정 불필요.
- **휩쓸기 판정(SweepAttackBehavior):** 타일 소속이 아니라 **월드 좌표 전방 부채꼴**(forward = 공격자 → 주 타깃, XZ 거리 ≤ `sweepReach` AND 각도 ≤ `sweepArcHalfAngle`). 월드 좌표는 `IEntityPositionProvider`(서버 권위). 아군/사망/공격자/주 타깃 제외, 건물 미대상.
- **튜닝 SO:** `SpecialAttackConfig`(Infrastructure/Config, `sweepReach`·`sweepArcHalfAngle`). GameBootstrapper가 SO 값을 읽어 핸들러에 **float로 주입**(Application → Infrastructure 역참조 회피). 현재 에셋은 Inspector에서 편집·배선하고 필요하면 기본 CreateAssetMenu로 생성한다. 2026-10-16 최소 유지 결정으로 과거 전용 생성기는 퇴역한다. ⚠️ 에셋 생성 ≠ 씬 배선 — 미배선 시 폴백값 사용. 이번 도구 정리는 현재 설정·주입 경로를 바꾸지 않는다.
- **AoE 연출 동시 방출:** `HitPresentationQueue`가 공격자 `HitFrameTimes.Length ≤ 1`이면 보류 큐 전부 방출(휩쓸기 N마리 동시 표시), `> 1`이면 신호당 1건(다중 히트 유닛 회귀 없음).
- 상세 규칙: `GameSystemRules_Units.md` 규칙 23~27. task: `_Tasks/2026-07-16/18_06_battleaxe-aoe/`.

### 건물 배치 시스템 (MVP Phase 1)

프로토타입 완료 후 첫 MVP 기능. 건물 배치 + 시각화만 구현 (자원/생산 시스템 미포함).

#### 건물 타입 (2026-05-17 26종 확장)
```csharp
// BuildingType enum — 26종
// Castle×3종족, MiningPost×3종족
// 종족별 생산라인 3단계: HumanBarracks1/2/3, AncientGrove1/2/3, PrimalSanctuary1/2/3
// AutoTower×3종족 (Human=CannonTower, Spirit=RuneSpire, Trans=VineTower)
//   ⚠️ 2026-08-10 정정: Trans 방어 타워는 VineTower다(이전 표기 "Trans=MistShrine"은 오류).
//   MistShrine은 방어 타워가 아니라 공격하지 않는 별도 힐 건물이며 HealShrine = 6 이라는
//   독립 enum 값을 쓴다(AutoTower = 2와 완전히 별개). 규칙:
//   GameSystemRules_Buildings.md — MistShrine 물안개 힐 시스템
//   (2026-08-12 구현 완료 / 에디터 싱글플레이 실기 검증 완료 · 멀티 미검증).
// 추가: ResearchLab (미구현)
//
// BuildingTypeHelper.cs (Domain 레이어):
//   IsProductionBuilding(type) — 생산 패널 표시 여부
//   GetStage(type)             — 건물 단계 (1/2/3)
//   GetNextStage(type)         — 업그레이드 대상 타입
//   CanUpgrade(type)           — 업그레이드 가능 여부
//   CanShowActionPanel(type)   — 비생산 건물 액션 패널 표시 여부
//
// 내부 구조 (2026-06-25 Phase 2):
//   생산건물 메타데이터를 단일 Dictionary<BuildingType, BuildingMeta> lookup table로 보유.
//   BuildingMeta = { IsProduction, Stage, NextStage }. 위 IsProductionBuilding/GetStage/
//   GetNextStage는 이 table을 TryGetValue로 조회만 하며(미등록=비생산), 신규 생산건물 추가 시
//   table에 한 행만 추가하면 세 메서드가 자동 정합. 공개 API 시그니처는 동일.
```

#### 건물 데이터 (IDamageable 패턴)
```csharp
public class BuildingData : IDamageable {
    public int Id { get; }              // 자동 발급
    public BuildingType Type { get; }   // 불변
    public TeamId Team { get; }         // 불변
    public HexCoord Position { get; }   // 불변
    public int MaxHp { get; }           // BuildingStats에서 결정
    public int Hp { get; private set; } // 피격 시 감소
    public bool IsAlive => Hp > 0;
    public void TakeDamage(int damage); // 데미지 적용
}
```

#### 건물 배치 흐름 (상세)

건물 배치 흐름은 `InputHandler`에서 시작하여 `UI`, `UseCase`, `Factory`를 거치는 단방향 데이터 흐름을 따릅니다.

1.  **입력 감지 (InputHandler)**
    -   플레이어가 UI가 아닌 지역을 클릭하면 `InputHandler.HandleClick`이 호출됩니다.
    -   클릭된 좌표의 타일이 현재 플레이어 소유의 비어있는 타일인지 `BuildingPlacementUseCase.CanPlaceBuilding`을 통해 검증합니다.
    -   조건이 맞으면, `BuildingPlacementUI.Show(coord, team)`를 호출하여 건물 선택 팝업을 띄웁니다.

2.  **UI 상호작용 (BuildingPlacementUI)**
    -   `Show()`가 호출되면 팝업 UI가 활성화됩니다.
    -   플레이어가 `BarracksButton` 또는 `MiningPostButton`을 클릭합니다.
    -   각 버튼의 `onClick` 이벤트는 `PlaceAndClose(BuildingType)` 메서드를 호출합니다.
    -   `PlaceAndClose`는 `BuildingPlacementUseCase.PlaceBuilding`을 호출하여 실제 배치 로직을 요청하고, 스스로 `Close()`를 호출하여 팝업을 닫습니다.
    -   (참고: 배경 클릭 시 팝업 닫기는 'UI 팝업 구현 패턴'을 따릅니다.)

3.  **로직 실행 (BuildingPlacementUseCase)**
    -   `PlaceBuilding(type, team, coord)`가 호출되면, 다시 한번 배치 가능 여부를 최종 검증합니다.
    -   `BuildingStats.GetMaxHp(type)`으로 타입별 기본 HP를 조회합니다.
    -   `BuildingData` 인스턴스를 생성합니다 (HP 포함).
    -   해당 타일의 상태를 '건설됨'으로 변경합니다 (`HexTile.HasBuilding = true`).
    -   `GameEvents.OnBuildingPlaced` 이벤트를 발행(OnNext)하여 시스템의 다른 부분에 건물 배치가 완료되었음을 알립니다.
    -   건물 파괴 시: `RemoveBuilding(id)` → Dictionary 제거 + `HexTile.HasBuilding = false`. 이동 가능 여부는 `TileKind`/`MineKind`와 함께 다시 계산되므로 영구 차단 지형이나 광산이 열리지 않습니다.

4.  **객체 생성 (BuildingFactory)**
    -   `BuildingFactory`는 `OnBuildingPlaced` 이벤트를 구독(Subscribe)하고 있습니다.
    -   이벤트를 수신하면, 전달받은 `BuildingData`에 맞는 건물 프리팹(`Building_Barracks.prefab` 등)을 `Instantiate`하여 월드에 생성합니다.
    -   생성된 게임 오브젝트의 `BuildingView` 컴포넌트에 `BuildingData`를 전달하여 초기화합니다.

5.  **자동 배치 (GameBootstrapper)**
    -   게임 시작 시 `GameBootstrapper.PlaceCastles` 메서드가 양 팀의 `Castle`을 지정된 위치에 자동으로 배치하며, 이는 `BuildingPlacementUseCase`를 통해 위와 유사한 로직을 실행합니다.

#### 렌더링 순서
3D 전환 이후 sortingOrder는 완전 폐기. Orthographic 55도 틸트 카메라의 Z-buffer(깊이 버퍼) 기반으로 타일/건물/유닛의 렌더링 순서가 자동 결정됨. XZ 평면 사용, Y축이 높이 방향.

#### 건물 관련 이벤트
```csharp
// 건물 배치 (BuildingPlacementUseCase → BuildingFactory)
GameEvents.OnBuildingPlaced.OnNext(new BuildingPlacedEvent(building));

// 건물 업그레이드 (BuildingPlacementUseCase → BuildingFactory, ProductionTicker 등)
GameEvents.OnBuildingUpgraded.OnNext(new BuildingUpgradedEvent(oldBuildingId, newBuilding));

// 건물 사망/철거 (BuildingPlacementUseCase → BuildingFactory)
GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(building));
// BuildingFactory가 _buildingObjects Dict에서 O(1) 조회 후 GO Destroy
// (BuildingView.cs 삭제됨 — 2026-05-18)
```

#### 영토 확장 (건물 건설 시)

건물 배치 시 배럭 인접 6타일을 건물 팀으로 자동 점령:
```csharp
// BuildingPlacementUseCase.PlaceBuilding() 내부
var neighbors = _grid.GetNeighbors(position);
foreach (var neighbor in neighbors)
{
    if (neighbor.Owner != team)
    {
        _grid.SetOwner(neighbor.Coord, team);
        GameEvents.OnTileOwnerChanged.OnNext(
            new TileOwnerChangedEvent(neighbor.Coord, team));
    }
}
```

### 유닛 생산 시스템 (MVP Phase 2)

배럭에서 유닛을 생산하는 핵심 게임플레이 루프.

#### 생산 관련 Domain 클래스

```csharp
// UnitProductionStats: 유닛 타입별 생산 시간/비용
public static class UnitProductionStats {
    public static float GetProductionTime(UnitType type) => type switch {
        UnitType.Pistoleer => 5f, _ => 5f
    };
    public static int GetGoldCost(UnitType type) => type switch {
        UnitType.Pistoleer => 50, _ => 50
    };
    public static int GetPopulationCost(UnitType type) => 1;
}

// ProductionState: 배럭 하나의 생산 상태 (2026-04-19 PendingQueue 구조로 재작성)
public class ProductionState {
    public int BarracksId;
    public List<QueueSlot> PendingQueue;    // 수동+자동 통합 단일 큐 (최대 3 가시 슬롯)
    public List<UnitType> AutoTypes;        // 자동 등록 타입 목록 (순환 반복)
    public bool IsAutoMode;
    public int AutoIndex;                   // 자동 순환 인덱스
    // CurrentIsAuto: AutoTypes에서 파생하는 getter (2026-06-05 수동 필드 → getter 전환)
    public bool CurrentIsAuto => PendingQueue.Count > 0 && AutoTypes.Contains(PendingQueue[0].UnitType);
    public HexCoord? RallyPoint;
}
// QueueSlot: UnitType, IsCharged(골드 차감 여부), Progress(생산 진행도)
```

#### UseCase 구조

| UseCase | 역할 |
|---------|------|
| `ResourceUseCase` | 팀별 골드 관리 (시작 500, 차감/추가/조회) |
| `PopulationUseCase` | 인구수 계산 (최대=보유 타일, 사용=건물+유닛) |
| `UnitProductionUseCase` | 생산 큐/타이머/자동-수동 모드/랠리포인트 |

#### 생산 흐름 (상세)
```
배럭 배치 → RegisterBarracks(BuildingData)
  ↓
플레이어 탭 → EnqueueUnit(barracksId, type)
  → 자동 모드 해제, 현재 자동 생산 취소 (골드 환불 없음)
  → ManualQueue에 추가
  → OnProductionQueueChanged 이벤트
  ↓
Tick(dt) — ProductionTicker가 매 프레임 호출
  → TryStartNext: ManualQueue[0] 또는 AutoTypes[AutoIndex]
  → 골드/인구 부족 시 대기
  → 충족 시: 골드 차감 → CurrentProducing 설정 → OnProductionStarted
  ↓
TickProduction(state, dt)
  → ElapsedTime += dt (RequiredTime 초과 방지 캡 처리)
  → Progress >= 1.0 → CompleteProduction()
  ↓
CompleteProduction(state)
  → FindSpawnTile(barracksPos) — 인접 이동 가능 + 유닛 없는 타일
  → 스폰 불가: 대기 (매 프레임 재시도, Progress 1.0 유지)
  → 스폰 가능: UnitSpawnUseCase.SpawnUnit()
  → 자동 모드: AutoIndex 순환
  → OnUnitProduced 이벤트 (랠리포인트 정보 포함)
```

#### 런타임 유닛 의존성 주입

UnitFactory에 의존성 참조를 저장하여 생산된 유닛에 자동 주입:
```csharp
// GameBootstrapper에서 한 번 호출
_unitFactory.SetDependencyReferences(config, movement, combat, unitFactory, buildingFactory);

// UnitFactory.CreateUnitObject() 내부에서 자동 적용
unitView.Initialize(unitData);
if (_hasDependencies)
    unitView.SetDependencies(config, movement, combat, unitFactory, buildingFactory);
```

#### 생산 이벤트
```csharp
// 자원 변경 (ResourceUseCase → UI)
GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(team, gold));

// 생산 시작 (UnitProductionUseCase → UI)
GameEvents.OnProductionStarted.OnNext(new ProductionStartedEvent(barracksId, type));

// 유닛 생산 완료 (UnitProductionUseCase → ProductionTicker)
GameEvents.OnUnitProduced.OnNext(new UnitProducedEvent(unit, rallyPoint));

// 큐 변경 (UnitProductionUseCase → UI)
GameEvents.OnProductionQueueChanged.OnNext(new ProductionQueueChangedEvent(barracksId));

// 랠리포인트 변경 (UnitProductionUseCase → ProductionTicker 마커 관리)
GameEvents.OnRallyPointChanged.OnNext(new RallyPointChangedEvent(barracksId, coord));
```

#### ProductionTicker (Presentation 브릿지)

순수 C# UseCase를 Unity Update 루프에 연결하는 MonoBehaviour:
```csharp
public class ProductionTicker : MonoBehaviour {
    void Update() {
        _productionUseCase?.Tick(Time.deltaTime);
        _resourceUseCase?.TickIncome(Time.deltaTime, ...);
        TickSiege(); // 1초 간격으로 공성 유닛 전진 체크
    }
    // OnUnitProduced 구독 → 랠리포인트 자동 이동 처리 (BFS 빈 타일 탐색)
    // OnRallyPointChanged 구독 → 마커 생성/이동/제거
    // OnEntityDied 구독 → 배럭 파괴 시 마커 Destroy + 공성 목록에서 제거
    // ShowRallyMarker/HideAllRallyMarkers — 팝업 연동
}
```

#### 공성 시스템 (Siege System)

생산된 유닛이 자동으로 적 Castle을 향해 진군하는 시스템. ProductionTicker에서 관리.

**진군 흐름:**
```
유닛 생산 완료 (OnUnitProduced)
  ↓
랠리포인트 설정됨?
  ├─ 예 → BFS 빈 타일 탐색 → 랠리포인트 근처로 이동
  │        ↓ OnMoveComplete 콜백
  │        적 Castle 방향 BFS 경로 탐색 → 이동
  └─ 아니오 → 적 Castle 방향 BFS 경로 탐색 → 직접 이동
  ↓
Castle 인접 도착 (또는 경로 상 정지)
  ↓
공성 목록(siegeUnits)에 등록
  ↓
매 1초 TickSiege()
  → Castle까지 BFS 거리 계산
  → 현재보다 가까운 빈 타일이 있으면 이동
  → Castle 인접(거리 1) 도달 시 공성 목록에서 제거 (더 이상 전진 불필요)
```

**공성 목록 관리:**
- 등록: Castle 방향 이동 완료 시 (OnMoveComplete 콜백)
- 제거 조건:
  1. Castle 인접 타일(거리 1) 도달
  2. 유닛 사망 (OnEntityDied 이벤트)
  3. GameObject 파괴 (null 체크)

**UnitView.OnMoveComplete 콜백:**
```csharp
// 이동 완료 시 1회 실행되는 콜백 (System.Action)
public System.Action OnMoveComplete { get; set; }
// MoveAlongPath 코루틴 종료 시 호출 → null로 초기화
// 용도: 랠리→Castle 체인 이동, 공성 목록 등록
```

#### 랠리포인트 마커 표시 규칙
- **설정 직후**: 마커 생성 + 3초간 표시 → 자동 숨김
- **배럭 선택(팝업 열림)**: 마커 표시 (ProductionPanelUI → ShowRallyMarker)
- **팝업 닫힘/다른 오브젝트 클릭**: 마커 숨김 (ProductionPanelUI → HideAllRallyMarkers)
- **배럭 타일에 랠리포인트 설정**: 랠리포인트 해제 + 마커 Destroy
- **배럭 파괴**: 마커 Destroy
- **마커 프리팹**: GameConfig.RallyPointPrefab (Inspector에서 할당)
- **마커 위치/회전**: GameConfig.RallyMarkerOffset / RallyMarkerEuler (Inspector 조정)

#### 랠리포인트 BFS 빈 타일 탐색
랠리포인트 타일이 점유 중일 때 유닛이 멈추는 문제를 방지하기 위해 BFS로 가장 가까운 빈 타일을 탐색:
```
Ring 0: 랠리포인트 자체 (1타일)
Ring 1: 인접 6타일
Ring 2: 그 바깥 12타일
Ring 3: 그 바깥 18타일 (최대 제한, maxRange=3)
```
- 각 타일에 대해 RequestMove 시도 → 성공하면 즉시 반환
- BFS 특성상 랠리포인트에 가장 가까운 빈 타일이 자동 선택
- 범위 내 빈 타일 없으면 이동 안 함

#### 생산 UI (ProductionPanelUI)

배럭 클릭 시 표시. 기존 UI 에셋(ui_panel_dark, ui_slot_queue, ui_bar_progress_frame 등) 활용.

**탭**: 수동 큐 추가 / **롱프레스(0.5초)**: 자동 생산 토글

#### GameConfig 경제 설정
```csharp
[Header("Economy")]
float MiningGoldPerSecond = 10f;  // 채굴소 초당 수입
int BarracksCost = 100;           // 배럭 건설 비용
int MiningPostCost = 50;          // 채굴소 건설 비용
```

> 🔴 **2026-09-14 — 위 블록에서 맵 테스트 모드 필드 2줄을 제거했다** (제거 근거의 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 3 의 개정 블록).
> ⚠️ **없어지는 것은 「테스트 모드일 때의 초기 골드」이며, 이 블록이 원래 적지 않고 있던 `Economy` 의 시작 골드 필드는 별개로 그대로 남는다.** 두 값이 **우연히 같아서** 테스트 모드를 켜도 화면이 달라지지 않았고, 그것이 이 기능이 오래 방치된 이유다 — 한쪽이 사라진다고 다른 쪽이 따라 사라진다고 읽지 말 것.
> ⚠️ **이 회차에는 `.cs` 와 `.asset` 을 한 줄도 바꾸지 않았다** — 설계 문서가 코드보다 앞서 있는 상태이며, 실제 필드 삭제는 `_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md` 의 T2 다.

---

<a id="성능-최적화"></a>

## ⚡ 성능 최적화

### 모바일 최적화 전략

#### 1. 오브젝트 풀링
```csharp
// 유닛, 이펙트 재사용
ObjectPool<Unit> unitPool = new ObjectPool<Unit>(unitPrefab, 50);
```

#### 2. 컬링
```csharp
// 화면 밖 유닛 렌더링 비활성화
if (!IsVisible()) {
    renderer.enabled = false;
}
```

#### 3. LOD (Level of Detail)
```
멀리: 간단한 모델
가까이: 디테일한 모델
```

#### 4. 배칭
```
- Static Batching: 배경, 타일
- Dynamic Batching: 유닛 (같은 머티리얼)
```

#### 5. Addressables
```csharp
// 동적 에셋 로딩
Addressables.LoadAssetAsync<GameObject>("Units/Soldier");
```

### 타겟 성능
- **FPS**: 60fps (모바일)
- **메모리**: 300MB 이하
- **배터리**: 1시간 플레이 = 20% 소모 이하

---

<a id="개발-환경"></a>

## 💻 개발 환경

### Unity 프로젝트 설정
```
Unity Version: 6000.0.x (Unity 6 LTS)
Template: 3D (URP)
Platform: Android / iOS
Scripting Backend: IL2CPP
API Level: Android 7.0+ (API 24)
Target Architectures: ARM64
```

### Git 설정
```gitignore
# .gitignore
Library/
Temp/
Obj/
Build/
Builds/
Logs/
UserSettings/
.vs/
*.csproj
*.sln
```

### 빌드 설정
```
Build Settings:
- Compression Method: LZ4
- Development Build: ✅ (개발 중)
- Split Application Binary: ✅ (100MB+)
```

### 개발용 강제 실패 플래그 (에디터 전용) — 2026-09-24 신설

**현재 메뉴 경로(2026-10-16):** `Hexiege > Debug > Failure` 아래의 `다음 재경기`, `다음 최초 경기`, `싱글 맵 준비 (다음 1회)`, `싱글 맵 투영 (다음 1회)`, `현재 설정 확인`, `해제`를 사용한다. 아래 날짜별 표의 `Hexiege/Debug/강제 실패 — ...`는 당시 경로다. 실패 종류·일회성 설정·해제 동작은 유지한다.

**[쉬운 말로]** 재경기를 눌렀을 때 **새 맵 준비가 실패하는 화면**은 평소에 볼 수가 없다. 맵을 주고받는 구간이 **실측 99~134ms** 라 사람이 그 0.1초에 맞춰 네트워크를 끊을 수 없기 때문이다. 그래서 **「다음 재경기를 반드시 실패시킨다」고 미리 켜 두는 스위치**를 만들었다. 미리 켜 두고 재경기를 누르면 확실히 실패하므로 **타이밍 문제가 사라진다.**

> 🔴 **이 절은 「도구를 어떻게 쓰는가」의 단일 소스다.** 무엇을 화면에 띄우는지(사양)는 규칙이 정한다 —
> [GameSystemRules/GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) 「공통 UI 규칙」의 **규칙 D-7 · 규칙 M-3 · 규칙 M-4**,
> 사유에 따른 문구 분기는 [GameSystemRules/GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md) **규칙 18** 이다. **여기에 옮겨 적지 않는다.**

**메뉴 경로** (Unity 에디터 상단 메뉴)

| 메뉴 | 무엇을 하는가 |
|------|-------------|
| `Hexiege/Debug/강제 실패 — 다음 재경기/① 연결 끊김 (Disconnected)` | 다음 재경기를 **즉시** 실패시킨다. 화면에는 **「상대방이 나가서…」 문구**가 떠야 한다 |
| `Hexiege/Debug/강제 실패 — 다음 재경기/② 해시 불일치 (HashMismatch)` | 다음 재경기를 **즉시** 실패시킨다. 화면에는 **기본 실패 문구**가 떠야 한다 |
| `Hexiege/Debug/강제 실패 — 다음 재경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)` | 즉시 실패시키지 않는다. **진짜 timeout·재전송 경로를 그대로 태운다**(아래 참조) |
| `Hexiege/Debug/강제 실패 — 현재 설정 확인` | 지금 예약돼 있는 사유를 **지우지 않고** 보여 준다 |
| `Hexiege/Debug/강제 실패 — 해제` | 예약을 지운다(예약이 없어도 안전하다) |

**사용 절차**

1. 🔴 **에디터를 Host(방을 만드는 쪽)로 둔다.** 실기기는 상대 역할이면 된다. **실패 판정은 Host 가 하고 문구도 그 화면에 뜨므로** 에디터 화면만 보면 된다.
2. 경기를 **정상적으로 시작해서 끝낸다.** 🔴 **최초 경기와 프로브에는 걸리지 않는다** — 최초 경기가 실패하면 게임 자체가 시작되지 않아 테스트가 불가능해지기 때문이다.
3. 결과 화면이 뜬 뒤(또는 그 전에) 위 메뉴에서 **사유 하나를 고른다.**
4. **재경기를 진행한다**(요청 → 수락, 또는 양쪽이 각자 「다시하기」). 그 회차가 실패하고 **결과 화면의 상태 줄**에 실패 문구가 뜬다.
5. 🔴 **한 번 쓰면 스스로 꺼진다.** 다시 보려면 **메뉴에서 다시 켠다.** 「켠 것을 잊어 모든 재경기가 계속 실패하는」 상황을 구조적으로 막기 위한 설계다.

**꼭 알아야 할 성질 5가지**

- 🔴 **릴리스 빌드에는 존재하지 않는다.** 저장소 클래스(`Infrastructure/Debug/ForcedMapTransferFailure.cs`)는 **파일 전체가 에디터 전용 컴파일 가드 안**에 있고, 전송 계층에서 이 플래그를 보는 블록도 같은 가드 안에 있다. 조작 수단이 **에디터 메뉴뿐**이라 에디터가 아닌 곳에서는 애초에 켤 수 없다.
- 🔴 **씬·프리팹에 저장되지 않는다.** 저장소는 `EditorPrefs` 이며 **직렬화 필드를 쓰지 않는다.** 이유 둘 — ① 이 프로젝트에서 **Inspector 값이 코드 기본값을 이겨 두 번 물렸다** ② 씬에 저장되면 **켠 채로 커밋될 수 있고 그러면 다른 사람도 실패를 겪는다.**
- 🔴 **재경기 회차에만 걸린다**(위 2번).
- ⚠️ **③ 응답 없음은 결과가 나오기까지 약 20초가 걸리는 것이 정상이다.** 즉시 실패시키지 않고 **그 회차의 Client 응답만 무시**해 **10초 대기 → 재전송 1회 → 다시 10초 → 실패**라는 진짜 경로를 태우기 때문이다. 🔴 **그래서 이 항목만이 결과 화면의 「맵 준비 한도」가 정말 두 창을 재는지 확인할 수 있다.**
- ⚠️ **즉시 실패 2종(① ②)은 기존 실패 경로를 그대로 탄다** — 강제 실패용 결말을 따로 만들지 않았다. 「테스트에서 본 화면」과 「실제로 나는 화면」이 달라지면 의미가 없기 때문이다.

**로그에서 진짜 실패와 구별하는 법**

- ① ② 는 결말 로그 메시지에 **`[강제 실패] … (실제 장애가 아니다)`** 가 들어간다.
- ⚠️ 🔴 **③ 은 운영 결말 줄에 강제 표식이 없다** — 진짜 timeout 코드를 그대로 타기 때문이다. **구분 근거는 회차 시작 시점에 남는 개발 로그(`Forced=ResponseTimeout`)** 한 줄이다.

> **상태 — ⚠️ 구현 완료 · 🔴 실기 사용 0회.** 이 도구로 실패 화면을 실제로 띄워 본 적은 아직 없다.
> **방식의 확정 경위와 실행 기록의 단일 소스는 [Plan.md](_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md) §8 과 §14** 다.

#### 🔴 사용 전에 Unity 의 Error Pause 를 끈다 (2026-09-27 추가 — 위 절차·성질 목록은 한 글자도 지우지 않는다)

**무엇을 해야 하나** — 이 플래그를 쓰기 전에 **Console 창의 `Error Pause` 를 꺼 둔다.** 켜져 있으면 **강제 실패가 발생한 순간 플레이 모드가 일시정지**되고, **멈춘 동안에는 코루틴이 돌지 않아 결과 화면의 카운트다운·맵 준비 한도가 전부 멈춘다.**

- 🔴 **왜 걸리는가 — 강제 실패의 결말 줄이 `[ERROR]` 레벨이다.** 위 「로그에서 진짜 실패와 구별하는 법」의 `[강제 실패] … (실제 장애가 아니다)` 줄이 그것이다. ⚠️ **이것은 결함이 아니다** — 이 도구는 **진짜 실패 경로를 그대로 타므로**(위 성질 5번째) 진짜 실패와 같은 레벨로 찍히는 것이 **설계대로**다.
- 🔴 **끄지 않으면 무엇을 오독하게 되는가 — 실제로 겪은 일이다.** 2026-09-27 이전 회차에 **「자동 복귀 카운트다운이 만료되지 않는다」**로 관측돼 코드에서 원인을 찾았고, *「복귀 메서드가 자기 코루틴을 멈춰 그 뒤 코드가 실행되지 않는다」* 는 가설까지 세워졌다. **실기 로그가 그 가설을 반증했다 — 멈춰 있던 것은 코드가 아니라 에디터였다.**
- ⚠️ **즉 이것은 코드 문제가 아니라 테스트 환경 문제다.** 🔴 **그래서 고칠 자리도 코드가 아니라 이 절차다.**
- **경위와 반증 근거의 단일 소스는 [Plan.md](_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md) §15-3** 이다.

> **[✅ 2026-09-27 갱신 — 위 「상태 — 실기 사용 0회」 줄은 2026-09-24 시점의 기록이라 한 글자도 지우지 않는다(`.claude/MEMORY.md` B-7).]**
> 🔴 **「실기 사용 0회」는 더 이상 참이 아니다 — 이 도구가 실제로 쓰였다.** 2026-09-27 로그에 `Forced=` 가 **7건**이고 **사유 3종이 전부 발화**했다(`Disconnected` 4 · `HashMismatch` 1 · `ResponseTimeout` 2. 문서 작업 중 내가 직접 셈).
> - ✅ **③ 만이 확인할 수 있다고 적어 둔 것이 확인됐다** — 응답 없음 회차에서 **대기 10초 → 재전송 1회 → 다시 10초 → 실패**(실측 **20.010초**)가 그대로 관측됐고, 결과 화면의 **맵 준비 한도 만료는 그 실패보다 9ms 앞섰다.** 즉 **한도가 정말 두 창을 잰다.**
> - ✅ **① 의 즉시 실패도 화면까지 이어졌다** — 실패 문구 2종을 **사용자가 화면으로 직접 확인**했다(근거 등급: 사용자 화면 확인).
> - ⚠️ **상태는 「구현 완료 · 실기 사용 확인」이지 「사양이 전부 검증됐다」가 아니다** — **실패 경로 중 「수락을 서버로 아예 보내지 못했다」는 이 도구로 만들 수 없다**(사유 3종은 전송 계층의 것이다). **검증 경계의 단일 소스는 [PROJECT_STATUS.md](PROJECT_STATUS.md) 의 「✅ 실기 검증 (2026-09-27)」 문단**이다. **[✅ 2026-09-28 (4차) 갱신 — 위 「이 도구로 만들 수 없다」는 더 이상 참이 아니다. 이 줄은 2026-09-27 시점의 기록이라 그대로 둔다. 단일 소스는 아래 「🔴 넷째 사유 추가」 소절]**

#### 🔴 넷째 사유 추가 — ④ 수락 전송 예외 (2026-09-28 (4차) 추가 — 위 절차·성질·메뉴 경로 목록은 한 글자도 지우지 않는다)

**[쉬운 말로]** 위 사유 3종(①②③)은 전부 **「맵을 주고받는 도중」**에 실패를 만든다. 그런데 재경기가 안 되는 길이 하나 더 있다 — **맵을 주고받기도 전에, 수락 자체를 서버로 보내지 못하는 경우**다. 이 경로는 ①②③ 으로는 만들 수 없어서 **넷째 스위치**를 따로 만들었다.

🔴 **이 절이 「④를 어떻게 쓰는가」의 단일 소스다.** 사양(무엇을 화면에 띄우는지)과 이 결함이 드러낸 새 버그는 여기에 옮겨 적지 않는다 — 규칙은 [GameSystemRules/GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) 「공통 UI 규칙」 규칙 D-7 의 2026-09-28 (4차) 확정 블록, 검증 결과·새 버그·수정 방향은 [Plan.md](_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md) §15-15 가 단일 소스다.

| 메뉴 | 무엇을 하는가 |
|------|-------------|
| `Hexiege/Debug/강제 실패 — 다음 재경기/④ 수락 전송 예외 (AcceptSendException)` | 다음 재경기의 **수락 전송 자체**를 예외로 끝낸다. 서버에 수락이 닿지 않으므로 **수락한 쪽 화면에만** 기본 실패 문구가 떠야 한다 |

- 🔴 **①②③ 과 계층이 다르다.** ①②③ 은 **맵 전송 계층**(`MapTransferErrorCode`)의 실패이고 저장소도 `Infrastructure/Debug/ForcedMapTransferFailure.cs` 하나를 공유한다. ④ 는 그보다 **앞 단계인 「수락 RPC 발신」** 에서 던지므로 **별도 클래스** `Infrastructure/Debug/ForcedAcceptSendFailure.cs` 로 뒀다 — `ForcedMapTransferFailure` 에 끼워 넣지 않았다. 성질은 같다(파일 전체 `#if UNITY_EDITOR` · `EditorPrefs` 저장 · `TryConsume()` 이 읽는 즉시 스스로 꺼지는 1회용).
- **주입 자리** — `NetworkGameEndController.SendAcceptRematchSafely()` 의 `try` 블록 안, **`AcceptRematchServerRpc()` 를 부르기 전**. 「보내는 순간 연결이 내려가는」 상황을 재현하려면 **전송 전**에 던져야 하고, 예외를 **기존 `catch` 경로**로 그대로 흘려보내 「테스트에서 본 화면」과 「실제로 나는 화면」이 같게 만든다(즉발 2종의 설계와 같은 이유).
- 🔴 **「현재 설정 확인」·「해제」 메뉴는 ①②③ 과 ④ 를 함께 다룬다** — 확인은 **켜져 있는 것을 전부** 나열하고, 해제는 **조건 없이 둘 다** 끈다. 새 메뉴가 아니라 **기존 두 메뉴의 대상 범위가 넓어진 것**이다.
- 사유 값은 `Cause=Unknown` 으로 들어온다(규칙 18 의 「모르면 중립 문구」 — 못 보낸 이유를 알 수 없기 때문이다).

**⚠️ Error Pause — ①②③ 과 다르다.**

위 「사용 전에 Unity 의 Error Pause 를 끈다」 절차는 **①②③ 에 대한 것**이다. ④ 는 다르다 — **결말 로그 두 줄이 모두 `[WARN]` 이고 `[ERROR]` 가 없다**(2026-09-28 (4차) 실기, `_Logs/_editor/2026-09-28/RuntimeLog.txt` 719~833행에 `[ERROR]` **0건** — 문서 작업 중 직접 확인). 🔴 **그 이유는 구조적이다** — ④ 는 새 결말 로그를 만들지 않고 **「수락을 서버로 보내지 못했을 때」를 원래부터 처리하던 기존 `catch` 경로**(원래 `Warn` 레벨)를 그대로 탄다. ①②③ 은 **진짜 맵 전송 실패 경로**(`[ERROR]` 레벨)를 그대로 타므로 다르다. ⚠️ **이번 확인은 1회 실기에 근거한다** — 앞으로도 걸리지 않을 것이라는 확정이 아니라, **그 결말 줄이 어느 로그 레벨을 타는지는 코드에 고정돼 있다**는 구조 근거와 이번 1회 관측을 함께 적어 둔다. **그래서 ④ 만 쓸 때는 Error Pause 를 끄는 준비 단계를 생략해도 된다.**

> **상태 — ⚠️ 구현 완료 · ✅ 실기 사용 확인 (2026-09-28, 1회).** `Forced=ForcedAcceptSendException` 이 로그에 1건 발화했다(문서 작업 중 직접 확인). **검증 결과와 이 도구가 드러낸 새 버그의 단일 소스는 [Plan.md](_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md) §15-15** 다.

#### 🔴 다섯째·여섯째 사유 — 싱글플레이 전용 2종 (2026-09-29 (2차) 추가 — 위 절차·성질·메뉴 경로 목록과 ④ 소절은 한 글자도 지우지 않는다)

**[쉬운 말로]** 위 네 개(①②③④)는 **전부 멀티플레이 재경기**에서만 걸린다. 그런데 혼자 하는 게임(싱글플레이)에서도 지도를 못 만들면 안내 화면이 떠야 하고, 그 화면 역시 평소에는 볼 수가 없다. 그래서 **싱글 전용 스위치 두 개**를 따로 만들었다. 지도를 쓰는 일이 **① 만든다 ② 격자에 새긴다** 두 단계라서 스위치도 두 개다.

🔴 **이 소절이 「⑤·⑥을 어떻게 쓰는가」의 단일 소스다.** 사양(무엇을 화면에 띄우는지)은 여기에 옮겨 적지 않는다 — [GameSystemRules/GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) 「공통 UI 규칙」 **규칙 M-4**(범위 확정은 그 규칙 끝의 2026-09-29 (2차) 블록, 검증 등급은 2026-09-29 (3차) 블록)와 **규칙 D-4**(싱글에는 자동 복귀 타이머가 없다)가 단일 소스다. 구현·실기 기록은 [Plan.md](_Tasks/2026-09-28/11_50_single-map-failure-ui/Plan.md) **§12 · §15 · §8-1** 이다.

| 메뉴 | 무엇을 하는가 |
|------|-------------|
| `Hexiege/Debug/강제 실패 — 싱글 맵 준비 (다음 1회)` | 다음 **싱글플레이** 맵 **준비**(설계도를 만드는 단계)를 실패시킨다 |
| `Hexiege/Debug/강제 실패 — 싱글 맵 투영 (다음 1회)` | 다음 **싱글플레이** 맵 **투영**(만든 설계도를 격자에 새기는 단계)을 실패시킨다 |

- **메뉴 항목은 이제 8개다** — 위 ①②③(3) + ④(1) + 「현재 설정 확인」·「해제」(2) + 싱글 2종(2). ⚠️ **새로 생긴 것은 싱글 2종뿐이고 나머지 6개는 그대로다.**
- 🔴 **「현재 설정 확인」·「해제」는 이제 저장소 **넷**을 함께 다룬다**(①②③ 맵 전송 사유 · ④ 수락 발신 · 싱글 맵 준비 · 싱글 맵 투영). **새 메뉴가 아니라 기존 두 메뉴의 대상 범위가 다시 넓어진 것**이다(④ 를 더할 때와 같은 방식).
- 🔴 **①~④ 와 다른 점 두 가지** — ⓐ **최초 경기에도 걸린다**(①~④ 는 재경기 회차에만 걸린다). 로비에서 싱글을 시작하면 **최초 경기 실패 화면(모달 팝업)**, 결과 화면에서 「다시하기」를 누르면 **다시하기 실패 화면(결과 화면이 되살아나고 상태 줄에 한 줄)** 이 나온다 — **먼저 오는 쪽**이 소비한다. ⓑ **멀티플레이에는 영향이 없다.**
- **성질은 ①~④ 와 같다** — 파일 전체가 에디터 전용 컴파일 가드 안이라 **릴리스 빌드에 존재하지 않고**, 저장소가 **에디터 환경설정**이라 씬·프리팹에 남지 않으며, **한 번 쓰면 스스로 꺼진다**(1회용).
- ⚠️ **두 화면(최초 경기 · 다시하기)을 다 보려면 각각 한 번씩 예약한다.**

**🔴 ⑥(투영)에 대해서만 반드시 알아야 할 것 — 준비와 투영을 동시에 예약하면 준비만 발동한다**

- 🔴 **두 항목을 함께 켜 두면 「준비」 쪽이 먼저 발동하고 「투영」은 **아예 불리지 않는다.** 이유는 도달 불가다** — 준비가 실패하면 부르는 쪽이 **곧바로 돌아가 투영 단계를 부르지 않기** 때문이다.
- ✅ **그래서 투영 예약은 소비되지 않고 그대로 남는다** — **다음 싱글 경기에서 발동한다.** 「현재 설정 확인」 메뉴가 둘이 함께 켜져 있으면 **그 사실을 한 줄로 알려 준다.**
- 🔴 **⑥의 저장소를 ⑤와 공유하지 않은 근거가 바로 이것이다.** 「담는 값의 종류가 다르다」(④ 를 따로 둔 근거)가 아니라 **「같은 키를 쓰면 준비 단계가 먼저 소비해 투영 단계가 영원히 발동하지 않는다」**는 **도달 불가**가 근거다.
- ⚠️ **그러므로 투영 화면을 보려면 「투영」만 켠다** — 확실하게 하려면 「해제」를 먼저 누르고 「투영」만 켠다.

**⚠️ Error Pause — ①②③ 과 같다(④ 와 다르다).**

- 🔴 **⑤·⑥ 은 쓰기 전에 Console 창의 `Error Pause` 를 꺼 둔다.** 결말 줄이 **`[ERROR]` 레벨**이기 때문이다 — 두 도구 모두 **진짜 실패 경로를 그대로 타므로**(맵 준비 실패 / 맵 투영 실패의 운영 로그) 진짜 실패와 같은 레벨로 찍히는 것이 **설계대로**다.
- 🔴 **끄지 않으면 무엇을 오독하는가는 이미 겪은 일이다** — 위 「사용 전에 Unity 의 Error Pause 를 끈다」 소절이 그 사례(2026-09-27, 「카운트다운이 만료되지 않는다」로 관측돼 코드에 없는 원인을 찾았다)의 단일 소스다. ⚠️ **④ 만 예외**이며(결말이 `[WARN]` 뿐이다) **⑤·⑥ 은 예외가 아니다.**

**로그에서 진짜 실패와 구별하는 법 (⑤·⑥)**

- 두 도구 모두 결말에 **`[강제 실패] …(실제 장애가 아니다)`** 가 들어간다 — **개발 로그 한 줄**(`Forced=` 필드 포함)과 **운영 실패 로그의 사유 문장** 두 겹이다.
- 🔴 **⑤와 ⑥을 서로 구별하는 것은 사유 문장의 낱말이다** — **「맵 준비」**인가 **「맵 투영」**인가. ⚠️ **화면은 두 경우가 완전히 같은 것이 정상이다**(규칙 M-4 가 사유를 문구로 가르지 않는다고 확정했다).
- ⚠️ **⑤ 에만 있는 표식이 하나 더 있다** — 실패 로그의 사유 코드가 **비어 있는 채로 실패**한다는 조합이며, 진짜 실패는 반드시 구체 사유 코드를 싣기 때문에 **정상 경로에서 나올 수 없는 조합**이다.

> **상태 — ⚠️ 구현 완료 · ✅ 실기 사용 확인.** ⑤ 는 **2026-09-29 에 2회 발화**(13:07:46.400 · 13:08:16.597), ⑥ 은 **2026-09-29 (2차) 에 2회 발화**(23:29:25.706 · 23:30:08.072) — 문서 작업 중 내가 로그 원본에서 직접 확인했다.
> ⚠️ **확인되지 않은 것** — 🔴 **정상 경로에서 이 두 단계가 실패한 기록은 0건**이라 **발생 빈도를 알 수 없다**(특히 투영 단계). ⚠️ **⑥ 은 에디터에서만 확인됐다**(실기기 로그에는 멀티 구간만 있다). ⚠️ **Unity 컴파일 경고 0건은 확인되지 않았다.**

---


> 개발 진행 현황 및 로드맵은 `ROADMAP.md`, `PROJECT_STATUS.md` 참조.

<a id="변경-이력"></a>

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 0.62.0 | 2026-10-13 | **멀티 최초 경기 준비 실패(규칙 M-2)가 새 사양으로 구현·실기 확인된 것을 반영했다 — 「복구 상태」 절의 첫 항목(최초 멀티 준비 실패)을 2026-10-12 사용자 확정 사양으로 교체하고, 그 구현 상태를 말하던 낡은 서술 3곳을 현재 사실로 고쳤다. 설계 구조(맵 전송 프로토콜·재경기 복구)는 바뀌지 않았다.** 🔴 **무엇이 바뀌었나** — ① 「복구 상태」 첫 항목: 종전 「연결 유지 · generic 모달 · 다시하기/로비로 · 양쪽 재시도 요청을 Host 가 한 번만 수락하고 새 root seed 로 재시작」 → **양쪽 즉시 이탈 + 알림 모달 「확인」 하나, 재시도 없음.** 문구·제목·버튼의 **단일 소스는 규칙 M-2** 이고 이 문서에 옮겨 적지 않았다(사양 개정은 2026-10-12 사용자 결정 — 규칙 M-2 본문의 2026-10-12 표시와 구현 계획서 9-5절). ② 「무작위 맵 시작 동기화」 절의 「여전히 남은 것 넷」 항목 ③ 과 2026-09-28 갱신 블록의 한 줄, 「복구 상태」 절 끝의 「코드에 구현이 없다」 — **구현됐다**로 고쳤다(종전 표시는 그 줄에 인용으로 남겼다). 🔴 **쓰지 않은 것** — 구현 구조와 실기 결과는 이 문서에 옮기지 않고 구현 계획서 14절을 가리켰다. ⚠️ **실기로 관측되지 않은 것**(스폰 전 실패 · 호스트의 완전한 사망 · 전송 계층 60초 통보)은 `PROJECT_STATUS.md` 2026-10-13 문단이 단일 소스다. |
| 0.61.0 | 2026-10-12 | **네트워크 동작 규정 세 절을 새 규칙 문서 `GameSystemRules/GameSystemRules_Network.md` 로 이관했다 — 본문은 한 글자도 고치지 않고 옮겼고, 설계 조항 변경 0건이다.** 🔴 **왜** — 이 세 절은 *사용자가 확정한 제품 동작 규정*인데 기술 설계서 안에 있어, 규칙을 찾는 사람이 가는 `GameSystemRules/` 를 다 읽어도 「플레이어는 자신이 Host 인지 Client 인지 알 수 없다」는 원칙을 모를 수 있는 배치였다. 실제로 그 원칙을 어긴 구현이 나왔다(멀티 최초 경기 맵 준비 실패 안내가 Host 화면에만 뜸 — 규칙 M-2). ① **이관한 절 3개** — 「🔴 최상위 원칙」(0.51.0 신설) · 「이탈 시 승패 기록 — 규칙 집합」(0.51.0 신설) · 「인게임 이탈 처리 — `ReconnectionHandler` 정리」(0.53.0 신설). ② **옮기지 않고 남긴 것** — Netcode 구조 · 동기화 전략 · 무작위 맵 시작 동기화 · **결과 화면 이탈 판정·통보 구조**(규정의 단일 소스가 이미 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17·18·19 라서 이 문서에는 구조·수치·실측만 남는다) · 치팅 방어. ③ **사본은 두지 않았다** — 「🌐 네트워크 설계」 맨 앞에 이관 안내 한 블록만 둔다. ④ **이 문서 안의 낡은 포인터 4곳**(「결과 화면 이탈 판정·통보 구조」 절 안의 2026-09-21 정정·보강 블록, 연결 타임아웃 표 아래의 「인게임 재접속 대기」 추가 블록, 2026-09-22 블록)을 새 위치로 고치고 〔2026-10-12 이관〕 조각을 달았다. 그중 한 곳은 *「이탈 시 승패 기록」의 확정 D* 라는 종전 표기의 귀속이 어긋나 있어(확정 D 는 인게임 이탈 처리 절에 있다) 사실대로 병기했다. ⑤ **아래 0.51.0 · 0.52.0 · 0.53.0 행은 이관 전 위치를 서술한 이력이라 고치지 않았다.** ⚠️ **이관한 본문 속 낡았을 수 있는 상태 표시(예: 「인터넷 도달 확인은 현재 구현돼 있지 않다」)는 이번에 고치지 않았다** — 이관과 갱신을 한 회차에 섞지 않았다. 경위·절별 판정 근거·포인터 전수는 `_Tasks/2026-10-12/15_28_network-rules-migration/Plan.md`. |
| 0.60.0 | 2026-09-30 | **「결과 화면 이탈 판정·통보 구조」 절의 낡은 문장 2곳에 갱신 포인터를 덧붙였다 — 원문 무삭제, 설계 조항 변경 0건.** 🔴 **고친 것이 아니라 덧붙인 것이다**(`.claude/MEMORY.md` B-7 — 절 제목과 해당 ⚠️ 줄은 한 글자도 지우지 않았다). ⓐ **절 제목의 「미구현」** — 이 구조는 「경기 종료 후 UI·프로세스」 **단계 6** 에서 구현됐고 2026-09-30 실기에서 무반응 이탈 판정과 그 뒤의 화면 처리가 관측됐다. ⓑ **「「응답이 없다」를 무엇으로 재는지는 미확정 — 1순위 RTT · 2순위 결과 화면 전용 하트비트」** — **2순위가 채택돼 구현됐다.** 결과 화면 전용 감시 시계가 일정 간격으로 신호를 주고받는 형태로 돌고 있으며, **1순위 RTT 는 이 판정에 쓰이지 않았다**(그 값을 읽는 자리가 여전히 핑 표시 한 곳뿐임을 문서 작업 중 직접 재측정). 🔴 **두 어긋남 모두 2026-09-30 회차가 만든 것이 아니라 단계 6(2026-09-27) 이후로 낡아 있던 것**이다. **단일 소스는 규칙 쪽을 가리켰다** — 판정 규정은 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17, 실기 관측 기록은 `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 D-6 의 2026-09-30 블록. ⚠️ **확인되지 않은 것 · 범위 밖** — 🔴 **규칙 17 의 ③ 표도 아직 「미확정」이라고 적고 있다**(같은 낡음이 규칙 쪽에 남아 있으며 이번 범위가 아니다 — `CLAUDE.md` 규칙 6). 절 제목의 「미구현」을 닫은 것이 **그 절의 모든 조항이 검증됐다는 뜻은 아니다**(근거 등급은 위 규칙 D-6 블록이 단일 소스). 🔴 **로그 키·필드 이름은 이 문서에 옮겨 적지 않았다** — 적으면 검증 grep 이 오염된다(`.claude/mistakes.md` 주석 사본 함정). |
| 0.59.0 | 2026-09-29 | **「개발용 강제 실패 플래그 (에디터 전용)」 절에 「🔴 다섯째·여섯째 사유 — 싱글플레이 전용 2종」 소절을 신설했다 — 설계 조항 변경 0건.** 이 절이 ①~④(전부 멀티 재경기)까지만 적고 있어, **2026-09-28 에 신설된 ⑤ 싱글 맵 준비**와 **2026-09-29 (2차) 에 신설된 ⑥ 싱글 맵 투영**이 문서에 없었다 — 둘 다 **실기에서 각각 2회 발화**한 상태였다(⑤ 13:07:46.400 · 13:08:16.597 / ⑥ 23:29:25.706 · 23:30:08.072. 문서 작업 중 내가 로그 원본에서 직접 확인). **함께 적은 것** — 메뉴 항목이 **8개**가 됐다는 것 · 「현재 설정 확인」·「해제」가 이제 **저장소 넷**을 함께 다룬다는 것 · 🔴 **①~④ 와 달리 최초 경기에도 걸린다**는 것 · 🔴 **준비와 투영을 동시에 예약하면 준비만 발동하고 투영은 불리지 않는다**는 것과 그 이유(**도달 불가** — 준비가 실패하면 투영이 아예 불리지 않으므로, 저장소를 공유하면 투영이 영원히 발동하지 않는다. ⑥ 의 저장소를 ⑤ 와 나눈 근거가 이것이며 ④ 를 따로 둔 근거와 다르다) · 🔴 **`Error Pause` 는 ①②③ 과 같이 꺼야 한다**(결말이 `[ERROR]` 레벨이다 — ④ 만 예외다) · ⑤ 와 ⑥ 을 로그에서 가르는 낱말(「맵 준비」 ↔ 「맵 투영」)과 화면이 같은 것이 정상인 이유. ⚠️ **사양은 여전히 규칙 M-4 · D-4 가 단일 소스이며 여기에 옮겨 적지 않았다.** ⚠️ **확인되지 않은 것** — 정상 경로에서 두 단계가 실패한 기록 **0건**(발생 빈도 미확정) · ⑥ 은 **에디터에서만** 확인 · **컴파일 경고 0건 미확인**. |
| 0.58.0 | 2026-09-28 | **「무작위 맵 시작 동기화」 절의 「여전히 남은 것 넷」 항목 ③ 에 갱신 블록을 덧붙였다 — 설계 조항 변경 0건, 항목 ③ 도 그 문장도 지우지 않았다.** 🔴 **낡은 조각은 *「countdown 전체 길이 재시작 미구현」* 하나다 — `GameEndUI` 에 전체 길이 재시작 전용 진입점이 있고(정의 1곳 · 호출 1곳, 이름으로 가리켰다) 실기 만료 실측치까지 「공통 UI 규칙」 규칙 D-7 의 2026-09-28 블록에 남아 있다.** ⚠️ **항목 ③ 이 통째로 해소된 것은 아니다 — 싱글플레이(규칙 M-4)의 맵 준비 실패 표시 UI 는 여전히 미구현**이며 그 작업의 단일 소스는 [Plan.md](_Tasks/2026-09-28/11_50_single-map-failure-ui/Plan.md) 다. **함께 적은 것** — 🔴 **싱글플레이 결과 화면에는 자동 로비 복귀 타이머가 없다는 2026-09-28 사용자 확정의 포인터**(적용 범위의 단일 소스는 `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 D-4 끝의 「적용 범위를 멀티플레이 한정으로」 블록이며 **값을 옮겨 적지 않았다**) 와 **그 확정이 코드 미반영이라는 사실**. ⚠️ **같은 항목의 다른 조각(「팝업 여부 미정」)과 항목 ①②④ 는 이번에 판정하지 않았다**(규칙 10). |
| 0.57.0 | 2026-09-28 | **「개발용 강제 실패 플래그 (에디터 전용)」 절에 「🔴 넷째 사유 추가 — ④ 수락 전송 예외」 소절을 신설했다 — 설계 조항 변경 0건.** 실패 경로 「수락을 서버로 아예 보내지 못했다」를 만들던 재현 수단이 없다고 위 0.55.0·0.56.0 행이 적어 둔 것이 **더 이상 참이 아니게 됐다**(두 행은 지우지 않았다) — `Infrastructure/Debug/ForcedAcceptSendFailure.cs`(①②③ 과 별도 클래스, 같은 성질) 와 메뉴 `Hexiege/Debug/강제 실패 — 다음 재경기/④ 수락 전송 예외`가 신설돼 이제 재현 가능하고, 2026-09-28 (4차) 실기에서 1회 발화했다. **함께 적은 것** — 주입 자리(`SendAcceptRematchSafely()` 의 `AcceptRematchServerRpc()` 호출 전) · 「현재 설정 확인」·「해제」 메뉴가 이제 ①②③④ 를 함께 다룬다는 것 · **Error Pause 차이**(①②③ 은 결말이 `[ERROR]` 라 꺼야 하지만, ④ 는 기존 `Warn` 레벨 `catch` 경로를 그대로 타 결말이 `[WARN]` 뿐이라 끄지 않아도 된다 — 2026-09-28 (4차) 실기 1회로 `[ERROR]` 0건 확인 + 구조 근거). ⚠️ **사양은 여전히 규칙 D-7·규칙 18 이 단일 소스이며 여기에 옮겨 적지 않았다.** ⚠️ **이 경로를 재현한 결과 드러난 새 버그(§15-6 과 같은 뿌리, 반대 순서)는 이 문서의 범위가 아니다 — 단일 소스는 [Plan.md](_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md) §15-15**. |
| 0.56.0 | 2026-09-27 | **「개발용 강제 실패 플래그 (에디터 전용)」 절에 🔴 「사용 전에 Unity 의 Error Pause 를 끈다」 절차를 추가하고 「실기 사용 0회」 표기를 갱신했다 — 설계 조항 변경 0건.** **왜 절차가 필요했나** — 강제 실패의 결말 줄이 **`[ERROR]` 레벨**(진짜 실패 경로를 그대로 타므로 설계대로다)이라 **Error Pause 가 켜져 있으면 플레이 모드가 멈추고 코루틴도 멈춘다.** 그 때문에 직전 회차에 **「자동 복귀 카운트다운이 만료되지 않는다」**로 관측돼 코드에서 원인을 찾았고 *「복귀 메서드가 자기 코루틴을 멈춘다」* 는 가설까지 세워졌는데, **2026-09-27 실기 로그가 그 가설을 반증했다 — 멈춰 있던 것은 코드가 아니라 에디터였다.** 🔴 **즉 코드 결함이 아니라 테스트 환경 문제이므로 고칠 자리가 코드가 아니라 이 절차다.** 함께 갱신 — **`Forced=` 7건 · 사유 3종 전부 발화**(위 0.55.0 행이 적은 「실기 사용 0회」는 더 이상 참이 아니며 **그 행은 지우지 않았다**) · **③ 응답 없음만이 확인할 수 있다고 적어 둔 「맵 준비 한도가 두 창을 재는가」가 실측으로 확인**(대기 10초 → 재전송 1회 → 다시 10초, UI 한도 만료가 실패보다 9ms 앞섰다). ⚠️ **사양은 여전히 규칙 D-7 · M-3 · M-4 와 규칙 18 이 단일 소스이며 여기에 옮겨 적지 않았다.** ⚠️ **「수락을 서버로 아예 보내지 못했다」 경로는 이 도구로 만들 수 없어 여전히 재현 수단이 없다.** |
| 0.55.0 | 2026-09-24 | **「💻 개발 환경」에 「개발용 강제 실패 플래그 (에디터 전용)」 절을 신설했다 — 설계 조항 변경 0건, 이 절은 「도구를 어떻게 쓰는가」의 단일 소스다.** 재경기 맵 준비 실패 화면은 전송 구간이 **실측 99~134ms** 라 정상 경로에서 관측되지 않는데, 그 화면을 재현하는 **에디터 메뉴 5개 · 사유 3종**이 구현돼(「경기 종료 후 UI·프로세스」 §8 14-4) **쓰는 절차를 사람이 찾을 수 있는 자리에 둘 필요가 생겼다.** 절에 담은 것 — 메뉴 경로 표 · 사용 절차 5단계(🔴 **에디터를 Host 로 둘 것** · **재경기 회차에만 걸린다** · **한 번 쓰면 꺼진다**) · 성질 5가지(릴리스 빌드에 존재하지 않음 · `EditorPrefs` 저장이라 씬·프리팹에 남지 않음 · 응답 없음 사유만 약 20초 · 즉시 실패 2종은 기존 실패 경로 그대로) · **로그에서 진짜 실패와 구별하는 법**. ⚠️ **사양(무엇을 띄우는가)은 규칙 D-7 · M-3 · M-4 와 규칙 18 이 단일 소스이며 여기에 옮겨 적지 않았다.** ⚠️ **상태는 「구현 완료 · 🔴 실기 사용 0회」다.** |
| 0.54.0 | 2026-09-22 | **2026-09-22 실기 테스트에서 드러난 사실 2건과 사용자 확정 1건을 기록했다 — 구현은 하지 않았다 (코드·에셋·씬 변경 0줄, 문서만).** ① 🔴 **「결과 화면 이탈 판정·통보 구조」 절 끝에 「`NetworkStatusUI` 가 어느 씬에도 배치돼 있지 않다」 블록을 신설**했다(이 사실의 **단일 소스**). guid 검색 0건 · 2026-09-22 에디터·실기 로그 양쪽에서 실행 로그 0건. **핑 표시가 동작하지 않고 「상대방이 연결을 끊었습니다」 팝업이 존재하지 않는다.** 🔴 **같은 블록에서 단계 7 회차의 진단 한 줄(*"그 팝업이 결과 화면 문구를 가릴 수 있다"*)을 정정**했다 — **그 팝업은 애초에 뜨지 않으므로 「지금 겹치고 있다」는 서술이 사실이 아니었다.** ⚠️ **억제 가드는 지우지 않는다**(나중에 배치하면 겹침이 실제로 발생하므로 유효한 예방이며, 코드는 한 줄도 건드리지 않았다). 🔴 **처리 방침(배치할 것인가 · 팝업이 필요한가)은 미확정**이고 작업 항목은 `ROADMAP.md` 의 새 행이다. ② **「인게임 이탈 처리 — `ReconnectionHandler` 정리」 절의 확정 D 에 「2026-09-22 실기 재현 확인」 블록을 덧붙였다** — 실기기(Host) 앱 강제 종료 시 남은 에디터(Client)에서 **끊김 감지·디스폰 3건 뒤 아무 판정도 없었다.** 🔴 **확정 D 가 예측한 상황이 그대로 재현됐으므로 바뀐 것은 근거의 강도뿐이고 설계·순서·범위는 아무것도 바뀌지 않았다.** ⚠️ `ForceWin` 수정(커밋 `5dadba1`)은 **반대 구성이라 이 테스트로 검증되지 않았다.** ③ **같은 절의 「작업 순서」에 사용자 확정을 덧붙였다** — 착수는 **「단계 6 다음」이 아니라 「결과 화면 작업(단계 8~10) 마무리 다음」**이며 🔴 **인게임 이탈은 단계 1~10 의 범위가 아니다.** **기존 서술은 한 줄도 지우지 않았다**(`.claude/MEMORY.md` B-7). |
| 0.53.0 | 2026-09-21 | **인게임 이탈 처리(`ReconnectionHandler`)의 사용자 확정 2건을 기록했다 — 구현은 하지 않았다 (코드·에셋·씬 변경 0줄, 문서만).** 🔴 **「인게임 이탈 처리 — `ReconnectionHandler` 정리」 절을 「이탈 시 승패 기록 — 규칙 집합」과 「치팅 방어」 사이에 신설**했고, **확정 D·E 의 단일 소스는 그 절**이다. ① **확정 D — 인게임 판정도 Host·Client 가 각자 돌린다.** 현재 `ReconnectionHandler` 는 **서버 전용**이라 「🔴 최상위 원칙」에 어긋난다(실측: `OnNetworkSpawn` **81행** `if (!IsServer) { enabled = false; return; }` → **Client 에서 컴포넌트가 아예 비활성** · `OnClientDisconnected` **138행** `if (clientId == NetworkManager.LocalClientId) return;` → **Host 자신의 이탈 미처리**). 🔴 **그래서 Host 가 인게임에서 사라지면 남은 Client 에게는 판정할 주체가 없다 — 결과 화면(단계 6)과 같은 모양의 문제이고 해법도 같다.** 🔴 **결과 화면과 결정적으로 다른 점은 「인게임은 코드가 이미 있다」**는 것이라 **`WORKFLOW.md` [4] 「기존 로직 제거 규칙」이 적용된다** — 검증 전까지 **주석 처리로 비활성화**, 최종 삭제는 **[6] 통과 후 · [7] 전**. ⚠️ **이 규칙을 이번 사이클에서 한 번 어긴 사건(`.claude/mistakes.md` 2026-09-21 항목)을 그 절에서 가리켰다.** 🔴 **작업 순서도 확정 — 단계 6 을 먼저 하고 그 다음 인게임을 정리한다**(같은 구조를 두 번 만들지 않기 위해서다). ② **확정 E — 판정 시점은 `t=60` 이고 그 뒤의 유예는 없앤다.** 현재는 **t=0~60 전송 계층 재시도(아무 시계도 돌지 않는다) → t=60 `OnClientDisconnectCallback` → t=60~90 `_reconnectWaitSeconds` 30초 대기 → t=90 강제 승리**인데, **t=60 에 승부를 결정하고 t>60 유예를 없앤다.** 근거는 **ⓐ 일시적 끊김이면 전송 계층이 t<60 동안 붙들고 있어 끊김 이벤트 자체가 나지 않으므로 60초가 이미 재접속 허용 시간**이고 **ⓑ 그 뒤 30초는 상태 복구가 없어 아무것도 못 하고 버리는 시간**이라는 것이다. **제거 대상은 `_reconnectWaitSeconds`(47행) · `WaitAndForceWin()`(194행) · `OnClientReconnected()` 취소 경로(170행) 한 덩어리**이며 **남는 동작은 「끊김 감지 → 강제 승리」 하나**다. ⚠️ **`OnClientReconnected` 의 `if (clientId != _disconnectedClientId) return;` 가드(173행)는 실측이지만, 🔴 「NGO 가 재접속에 새 ClientId 를 준다」는 확인하지 않았다** — 그래서 **「취소가 동작하지 않는다」고 적지 않았다**(규칙 10). ⚠️ **`_reconnectWaitSeconds = 30f` 는 Phase 8 의 초기 기본값이고 이 값을 정한 논의 기록은 찾지 못했다**(「없다고 증명했다」가 아니다). **씬 값이 `Game.unity` 20230행에 `30` 으로 직렬화돼 있어 코드만 고치면 동작이 바뀌지 않는다.** ③ **기존 두 절에 포인터만 덧붙였다 — 원문 무삭제.** ⓐ **「이탈 시 승패 기록」 절의 「회선 끊김·앱 강제 종료 → 남은 쪽 60초 뒤 승리」가 곧 이 `t=60` 임을 이어 붙였다**(종전에는 그 60초가 절 안에서만 설명돼 `ReconnectionHandler` 와 연결돼 있지 않았다). ⓑ **「혼동 주의」 표의 「인게임 재접속 대기 30초」가 확정 E 로 없어질 예정**임을 덧붙였다(**코드는 아직 그대로이므로 표는 현재 상태로서 여전히 참**이다). ⓒ **「🔴 최상위 원칙」 절에 인게임 적용 포인터**를 달고, 그 절의 **「79행」 표기가 재측정 결과 81행**임을 **원문 무삭제로 정정**했다. 🔴 **`ROADMAP.md` 「재접속 실제 구현」 행의 「인게임 재접속 대기 시간 값 미확정(120초 제안·보류)」과 섞지 않는다 — 확정 E 는 그 미확정 항목을 닫지 않는다.** **작업 항목은 `ROADMAP.md` 의 「인게임 이탈 처리 정리(`ReconnectionHandler`)」 행**이다. **[문서 머리말]** `**버전:**` 0.52.0 → 0.53.0. **최종 수정일은 2026-09-21 로 같은 날이라 그대로다.** **기존 절과 과거 행은 한 글자도 고치지 않았다.** |
| 0.52.0 | 2026-09-21 | **「결과 화면 무반응 이탈 자체 감시」(단계 6)의 설계 확정 3건을 기록했다 — 구현은 하지 않았다 (코드·에셋·씬 변경 0줄, 문서만).** ① 🔴 **「결과 화면 이탈 판정·통보 구조」 절의 「무반응 이탈 = 서버(Host) 자체 감시」가 뒤집혔다 — Host 와 Client 가 각자 자기 쪽에서 판정한다.** 서버 전용이면 **Host 가 사라졌을 때 남은 Client 는 아무 통보도 받지 못하기** 때문이며(감시할 서버도 RPC 도 함께 사라진다) 이는 **「🔴 최상위 원칙」 위반**이다. ✅ **정상 퇴장 통보 RPC 는 그대로 둔다 — 자체 감시는 그것을 대체하는 것이 아니라 통보가 오지 못한 경우를 받는 그물이다.** **바뀌는 것은 표의 「감지 주체」 한 칸이고 30초와 「판정 시 연결 종료」는 그대로**다. **원문 표와 항목은 한 글자도 지우지 않고 정정 블록을 덧붙였다**(`.claude/MEMORY.md` B-7). ② 🔴 **「이탈 판정 절차」(내 인터넷 도달 확인)가 결과 화면 이탈 판정(`GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17)에도 적용된다는 것을 명시**했다 — 종전에는 **승패 기록 문맥에만** 적혀 있었다. **절차는 공유하고 판정 뒤에 하는 일만 다르다**(인게임 = 승패 기록 / 결과 화면 = 연결 종료·화면 정리). 🔴 **도달 확인 구현은 단계 6 의 범위**이며 전적 기록 작업(별건)이 나중에 재사용한다 — 근거는 **규칙 17 이 「이탈로 판정하면 연결도 함께 종료한다」고 정하고 있어 오판이 곧 「돌아올 수 있었던 연결을 스스로 끊는 것」이 되기 때문**이다. **이 절의 표와 절차 자체는 바뀌지 않았다.** ③ ⚠️ **「응답이 없다」를 무엇으로 재는지는 미확정으로 적었다** — **1순위 기존 RTT(`Infrastructure/Network/NetworkGameManager.cs` 의 `GetCurrentRttMs()`, 현재 호출처는 `NetworkStatusUI` 의 핑 표시 한 곳) · 2순위 결과 화면 전용 하트비트**라는 **순서만** 확정됐고, 🔴 **RTT 로 「갱신이 멈췄다」를 판별할 수 있는지는 확인되지 않았다**(규칙 10). **확인 시점은 단계 6 착수 시**다. **기획 규칙 쪽 단일 소스는 규칙 17 의 「2026-09-21 추가 (2차)」 블록**이고, **계획 쪽은 `_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md` §2-1** 이다. **[문서 머리말]** `**버전:**` 0.51.0 → 0.52.0. **최종 수정일은 2026-09-21 로 같은 날이라 그대로다.** **기존 절과 과거 행은 한 글자도 고치지 않았다.** |
| 0.51.0 | 2026-09-21 | **사용자 확정 2건을 기록했다 — 구현은 하지 않았다 (코드·에셋·씬 변경 0줄, 문서만).** ① 🔴 **「최상위 원칙 — 플레이어는 자신이 Host 인지 Client 인지 알 수 없다」 절을 「🌐 네트워크 설계」 맨 앞에 신설**했다. 화면·승패 기록·이탈 처리가 **역할과 무관하게 항상 같아야 한다**는 규정이며, **개별 규칙이 이 원칙과 충돌하면 고칠 대상은 개별 규칙**이라고 못 박았다. 근거로 **메인 세션이 코드로 확인한 비대칭 2건**을 적었다 — `ReconnectionHandler.cs` **79행** `if (!IsServer) { enabled = false; return; }`(서버에서만 동작) · 같은 파일 `if (clientId == NetworkManager.LocalClientId) return;`(Host 자신의 이탈 미처리) · `RelayManager.cs` **66행/138행**(Host 가 나가면 Relay 할당과 Lobby 가 사라진다). **이 원칙의 단일 소스는 이 절**이며 `GameSystemRules_UI.md` 「공통 UI 규칙」과 `GameSystemRules_RandomMap.md` 규칙 17 은 **포인터만** 갖는다. ② **「이탈 시 승패 기록 — 규칙 집합」 절을 「결과 화면 이탈 판정·통보 구조」와 「치팅 방어」 사이에 신설**했다. **로비 버튼/앱 정상 종료 = 나가는 쪽이 스스로 패배 기록 + 남은 쪽 즉시 승리**, **회선 끊김·앱 강제 종료 = 남은 쪽 60초 뒤 승리**이며, 🔴 **앱 강제 종료는 다음 실행 시 「진행 중」 표시로 감지해 패배를 기록**한다(없으면 「강제 종료 = 패배 회피」가 남는다). 🔴 **남는 구멍을 미해결로 명시**했다 — 회선만 끊고 앱을 켜 두면 **양쪽 다 승리를 기록**하며 **서버 권한 없이는 막을 수 없다.** ⚠️ **전적을 쓰는 경로는 찾지 못했다**(`PlayerProfileService.cs` 는 `wins`/`losses` 를 `GetInt` 로 **읽기만** 하고 `SaveAsync` 2곳은 닉네임 저장 경로 — 문서 작업 중 직접 확인). 🔴 **「찾지 못했다」이지 「없다고 증명했다」가 아니다**(규칙 10). ⚠️ **60초가 어느 시계인지는 확정하지 않았다** — 연결 타임아웃 60초와 숫자만 같고, 규칙 17 의 판정은 30초다. **실제 작업 항목은 `ROADMAP.md` 「전적 기록 연결 + 이탈 시 승패 처리」 행**이고 **규칙의 단일 소스는 이 절**이다. **[🔴 같은 날 보완 — 위 ② 의 서술은 그대로 두고 덧붙인다]** 🔴 **「남는 구멍」에 적었던 「기기 혼자서는 구분할 수 없다 · 서버 권한 없이는 막을 수 없다」가 과한 진술이어서 정정 블록을 그 자리에 덧붙였다**(원문 무삭제, B-7). **상대와 무관한 제3자(인터넷)에 닿는지 확인하면 갈린다**는 것이 사용자와의 논의로 확정됐다 — **내 인터넷이 되는데 상대 신호만 없으면 상대 이탈(내 승리), 내 인터넷도 안 되면 내 이탈(내 패배)** 이며 **Host·Client 가 같은 절차**를 밟는다. 확인 대상 후보는 **UGS(Relay·Lobby·Cloud Save)**. ⚠️ **실측**: `Assets/_Project/Scripts` 에 `internetReachability`·`NetworkReachability`·`IsConnectedToInternet` **0건**(도달 확인 미구현) · `NetworkStatusUI.cs` 는 **핑 표시와 연결 끊김 팝업만** 담당(문서 작업 중 직접 확인). ⚠️ **일반 기술 사실(이 프로젝트 실측 아님)**: `Application.internetReachability` 는 **경로 유무만** 알려 준다. 🔴 **그 결과 남는 구멍은 2가지로 줄었다** — ⓐ **Relay 장애**(사용자 확정으로 **별건**이며 `ROADMAP.md` 「Relay 경로 장애 처리」 행이 맡는다. **승패 위조와 묶지 않는다**. 무효 경기 처리는 **메인 세션의 제안이고 확정이 아니다**) ⓑ **게임 트래픽만 선택 차단**(Relay 주소가 매 경기 바뀌어 난이도가 높다 · 서버 검증으로 푸는 대상). ③ 🔴 **「60초가 어느 시계인지 미확정」 표기를 닫았다 — 다른 시계다.** **인게임 60초 = 승패를 결정·기록**하며 **전송 계층 타임아웃(`m_DisconnectTimeoutMS: 60000`)과 같은 시계**다(숫자가 우연히 같은 것이 아니라 **끊김을 알리는 그 순간이 판정 시점**). **결과 화면 30초(규칙 17)는 화면을 정리하는 시계**이고 그 화면에서는 **승패가 이미 결정돼 있어 바뀌지 않는다.** 원문은 **취소선으로 남기고 지우지 않았다.** ④ ✅ **「전송 계층 연결 타임아웃 — 30초 → 60초」 절의 「씬 값은 바꾸지 않았다 — 기록만 했다」에 적용 결과를 덧붙였다** — 커밋 `007f666` 으로 `Game.unity` **45473행** · `Lobby.unity` **7479행** 이 **둘 다 `60000`** 임을 직접 확인했다. **원문(2026-09-16 시점의 기록)은 그대로 두었고** 「씬 값 미적용」으로 읽지 말라는 문장을 덧붙였다. **[문서 머리말]** `**버전:**` 0.50.0 → 0.51.0, `**최종 수정일:**` 2026-09-16 → 2026-09-21. **기존 절과 과거 행은 한 글자도 고치지 않았다.** |
| 0.50.0 | 2026-09-16 | **결과 화면 이탈 판정·통보 구조를 신설하고, 오해를 부르던 축약 표기 한 자리를 다듬었다 (코드·에셋·씬 변경 0줄 — 문서만).** ① **「결과 화면 이탈 판정·통보 구조」 절 신설**(「재경기 `MapDefinition` 생명주기」와 「치팅 방어」 사이). 정상 퇴장은 **나가기 전 통보로 즉시** 알리고, 무반응 이탈은 **서버 자체 감시 30초**로 판정하며 **판정과 동시에 연결을 종료**한다. **Host 이탈도 같은 통보 하나로 흡수**한다. 🔴 **NGO 명명 규약**(메서드명이 `ServerRpc`/`ClientRpc` 로 끝난다)과 **레이어 경계**(`NetworkBehaviour` 는 Infrastructure · Application 은 `Unity.Netcode` 직접 참조 금지 → `GameEvents` 경유)를 그 절에 못 박았고, 이름은 **규약을 지키는 예시**로만 적었다(확정 아님). ② 🔴 **전송 계층 연결 타임아웃 `m_DisconnectTimeoutMS` 를 30초 → 60초로 올린다는 결정과 그 근거를 기록**했다 — **판정(30초)보다 길어야 판정이 먼저 나기 때문**이며, 짧으면 전송 계층이 먼저 끊어 통보가 나갈 자리가 사라진다. 대상은 `Game.unity` · `Lobby.unity` **두 씬**이다. ⚠️ **씬 값은 이번에 고치지 않았다 — 기록만 했다.** ③ **같은 숫자에 묶이지 않도록 「다른 30초」를 표로 갈라 적었다** — 맵 전송 응답 대기 **10초**(규칙 16) · **인게임 재접속 대기 30초 → 강제 승리**(`ReconnectionHandler.cs:47` `_reconnectWaitSeconds = 30f`, 문서 작업 중 직접 실측) · 자동 로비 복귀 카운트다운 30초. 🔴 **그중 `ReconnectionHandler.OnClientDisconnected`(`:134`)에 경기 종료 가드가 없다는 실측을 「확인되지 않은 것」으로 남겼다** — 결과 화면 위에서 두 30초 시계가 겹치는지는 **확인하지 않았고 겹친다고도 안 겹친다고도 적지 않았다**(CLAUDE.md 규칙 10). ④ **맵 전송 중 이탈은 규칙 16 의 기존 실패 경로를 그대로 쓴다** — 🔴 **새 메시지도 새 `LogEvent` 키도 만들지 않고 실패 사유가 `Disconnected` 일 때의 화면 문구만 가른다.** ⑤ **씬 재로드 중 이탈은 범위 밖**이며 근거의 단일 소스는 규칙 19 다. ⑥ 🔴 **「동기화 대상」 표의 `persistent \`NetworkMapTransfer\`` 축약 표기를 고쳤다** — 정의 절 원문에서 **`persistent` 가 꾸미는 것은 `network connection` 이지 `NetworkMapTransfer` 가 아닌데**, 표의 그 줄만 읽으면 「객체가 계속 살아 있어야 한다」로 오해된다. 요구는 **「씬 객체에 귀속시키지 않는다」**이고 회차마다 동적으로 스폰하는 현재 구현은 그 규정을 **만족한다.** **정의 절(「`NetworkMapTransfer` 전송 프로토콜」) 원문과 개정 이력 `0.33.0` 행은 한 글자도 고치지 않았다.** 이 해석을 열어 두었던 자리는 `_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md` **§14-2** 이며 그 항목에 해소를 덧붙였다. **기획·규칙 쪽 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17·18·19**(이번에 신설)이고, 화면 문구·버튼·타이머는 `GameSystemRules_UI.md` 「공통 UI 규칙」이다 — **이 표에 옮겨 적지 않는다.** **[문서 머리말]** `**버전:**` 0.49.0 → 0.50.0, `**최종 수정일:**` 2026-09-15 → 2026-09-16. **과거 행은 손대지 않았다.** |
| 0.49.0 | 2026-09-15 | **문서 내용은 바뀌지 않았고 「구현 상태」 표기만 갱신했다 — 설계 조항 0건 변경.** 0.48.0 이 설계에서 걷어낸 맵 테스트 모드가 **코드에서도 실제로 제거**됐고(커밋 `8c83317` — 12개 파일 + `Resources/Config/GameConfig.asset` 의 `_mapTestModeEnabled`·`_testStartingGold` 두 행), **재경기 맵이 구현·실기 검증까지 끝났다**(커밋 `20ba0ab`). 그래서 **「무작위 맵 시작 동기화」 절 제목의 `(확정 설계, 1단계만 구현)` → `(확정 설계, 1~3단계 구현 · 잔여 있음)`** 으로 고치고 그 절 맨 앞에 **2026-09-15 상태 블록**을 덧붙였다. **2026-09-03 블록은 한 글자도 고치지 않았다** — 그 시점의 기록이고, 현재 상태는 새 블록이 답한다. 🔴 **새 블록이 못 박은 것 넷**: ① 2026-09-03 블록의 *"이 절의 맵 생성·검증·전송·해시 대조는 전부 미구현으로 읽어야 한다"* 는 **더 이상 사실이 아니다** ② 2단계 목록의 **「테스트 모드 설정 필드」에 달린 「삭제 대상」 표식은 이제 「삭제 완료」로 읽는다** — 항목을 지우지 않는 이유는 **끝난 단계의 범위 목록이 기록이기 때문**이다 ③ **`MapVersion` `1` → `2`** 와 **canonical 바이트 수 공식 `315 + 4N`**(N = 중립 광산 수). ⚠️ **「범위 경계」 문단의 *"초기값 `1`"* 은 고치지 않았다** — 그것은 **초기값의 기록이라 버전이 올라가도 참**이고, **현재 값을 적는 자리는 「전송 package」 절**이며 그쪽은 0.48.0 예고대로 T2 에서 이미 `2` 로 고쳐져 있다(0.48.0 이 *"값을 일부러 바꾸지 않은 자리"* 로 표시해 둔 두 곳이 이번에 **둘 다 제 몫대로 처리**됐다 — 하나는 값 교체, 하나는 표식 해제) ④ **여전히 남은 잔여 4건**(초기 골드 적용 · 조각 크기 실측 2건 · 실패 표시 UI · 경로 완전 차단)과 **구현은 됐으나 실기 미발화인 경로 3종**(전송 실패 복구 · 폴백 사용 · timeout/재전송)을 갈라 적었다. 🔴 **「구현했다」와 「실기에서 확인됐다」를 섞지 않는 것이 이 행의 요점**이며, 검증 경계의 단일 소스는 `PROJECT_STATUS.md` 의 「✅ 구현 완료 (2026-09-15)」 문단이다. **[문서 머리말]** `**버전:**` 0.48.0 → 0.49.0, `**최종 수정일:**` 2026-09-15. **과거 행은 손대지 않았다.** |
| 0.48.0 | 2026-09-14 | **맵 테스트 모드를 설계에서 제거 — `InitialGold` 는 `NeutralMineCount` 하나에서 파생하는 단일 갈래가 됐다 (사용자 확정, 코드·에셋 변경 0줄).** 🔴 **사용자 확정**: *"맵 테스트 모드는 현재 특별히 사용하고 있지 않아서 불필요한 것으로 보인다. 관련 구현 내용 삭제하고 문서도 삭제하면 될 것 같다."* 이 문서에서 고친 자리는 **여덟 곳**이다 — ① 「무작위 맵 시작 동기화」 도입부의 `MapTestModeEnabled=true` 갈래 서술 ② 같은 절 전달 항목의 「테스트 모드 표식(고정폭 0/1)」 ③ 같은 절 마지막 줄의 **「싱글은 로컬 `GameConfig` 의 테스트 모드를 적용 / 멀티는 Host 표식이 권위」**(🔴 **그 두 문장이 가리키던 설정 유래 입력이 표식 하나뿐이었으므로**, 남는 권위 규정인 「Host 가 생성·확정한 `MapDefinition` 이 권위」로 바꿔 적었다 — 실측 근거는 `MapPreparationUseCase.Prepare(ulong rootSeed, bool mapTestModeEnabled)` 의 인자 구성이다) ④ 「`MapDefinition` 정규 데이터 계약」 **상위 필드 목록의 표식**(🔴 **canonical 바이트 형식의 변경이므로 그 자리에 형식·버전·템플릿·재검증의 연쇄를 적은 주를 달았다**) ⑤ 「결정적 PRNG 및 독립 스트림 계약」의 `TestStartingGold(5000)` 갈래와 불변 선택값 목록의 표식 ⑥ 「deterministic fallback 정의」의 *"테스트 모드 표식과 그때의 `InitialGold` 규정은 대체 대상이 아니다"* → **「대체되지 않는 것은 `MapType` 하나」** ⑦ 「재경기 맵 흐름」 2번의 준비 시점 표식 적용 ⑧ 「GameConfig 경제 설정」 코드 블록의 필드 2줄. 🔴 **값을 일부러 바꾸지 않은 자리 2곳을 함께 표시했다** — 「전송 package」의 **`현재 지원 값은 1이다`** 는 「초기값」이 아니라 「현재 값」이라 형식 변경 시 거짓이 되므로 **미리 고치지 않고 T2 에서 함께 고친다는 표시**를 달았고, 3단계 범위 목록의 「테스트 모드 설정 필드」 항목은 **2단계에서 실제로 추가됐던 기록**이라 지우지 않고 삭제 대상 표시만 붙였다. ⚠️ **오해 방지** — 사라지는 것은 테스트 모드의 `TestStartingGold` 이고, `Economy` 의 시작 골드 필드는 **값이 우연히 같을 뿐인 별개 필드로 그대로 남는다.** 그 우연 때문에 테스트 모드를 켜도 화면이 달라지지 않았고 그것이 이 기능이 방치된 이유다. **제거 조항과 근거의 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 3 의 개정 블록**이며 이 표에 옮겨 적지 않는다. **이 회차는 문서만 바꿨다** — 코드 제거·`MapVersion` 상향·폴백 템플릿 5개 재생성·전송 실기 재검증의 단계 분할은 `_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md` 의 T1~T4 다. ⚠️ **위 0.43.0 · 0.42.0 등 과거 행은 손대지 않았다** — 그 행들이 적은 테스트 모드 확정은 그 시점에 실제로 확정돼 있던 내용이고, 고치면 이력이 거짓이 된다. |
| 0.47.0 | 2026-09-14 | **재경기 맵 조건 선택(`RematchMapMode`)을 설계에서 제거 — 재경기는 매 판 새 맵 준비 한 갈래다 (사용자 지시에 따른 개정, 코드 변경 0줄).** ① 「재경기 `MapDefinition` 생명주기」 절에서 **`RematchMapMode.SameMap`/`NewMap` enum · 컨텍스트의 mode 보관 · 상대 팝업의 조건 표시 · 「서로 다른 mode 교차 시 자동 시작 금지」 2개 조항 · `SameMap` 흐름 3단계**를 제거하고 「`NewMap` 흐름」을 **「재경기 맵 흐름」**으로 고쳐 남겼다. ② **흐름 3번(「후보 hash 가 현재 hash 와 같으면 폐기하고 새 root seed 부터 반복」)을 삭제**하고 이후 단계를 4~8 → 3~7 로 당겼다 — canonical byte 선두에 `MapVersion`·`RootSeed` 가 들어가는 구조상 **정상 경로에서는 결코 걸리지 않고 실제로 걸리는 경로는 폴백 템플릿뿐**이며, 폴백 연속 사용은 재시도로 감추지 않고 `MapPreparationUsedFallbackTemplate`(Warn)로 드러내기로 했다. ③ 「복구 상태」의 `NewMap 실패`·`single NewMap 실패` 항목과 `SameMap/NewMap/Lobby actions 재활성화` 를 **「재경기 맵 준비 실패」·「결과 화면의 기존 actions 재활성화」**로 바꿨다(**뜻은 그대로 — 실패 이전 화면으로 되돌린다**). ④ 「`NetworkMapTransfer` 전송 프로토콜」의 `최초 경기와 RematchMapMode.NewMap` → **「최초 경기와 재경기 맵 준비」**. ⑤ 3단계 범위 서술의 `SameMap`/`NewMap` 재경기 → **재경기 맵**(단계 경계 자체는 무변경). ⑥ 싱글 재경기 서술을 **「매 판 새 맵을 준비한다」**로 바꿨다 — **새 사양이 아니라 현재 코드가 이미 하는 동작**이다(`GameEndUI.OnRestartClicked` → `GameBootstrapper.LoadMap` → `MapRootSeed.Create()`, 직접 실측). ⑦ 🔴 **「2필드 구조 폐기」 블록(0.44.0 계열 서술)에는 삭제가 아니라 정정 블록을 덧붙였다** — 해시의 세 용도 중 「`NewMap` 재경기의 동일 맵 판정」만 사라지고 **Host/Client 비교·폴백 유효성은 그대로 남아 그 블록의 결론이 유지**되기 때문이다. **기획·규칙 쪽 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 14 의 개정 블록**이다. **[문서 머리말 정정]** 이 표는 0.46.0 까지 와 있는데 문서 상단 `**버전:**` 이 **0.43.2** 에 멈춰 있어(0.44.0~0.46.0 세 번의 갱신이 반영되지 않았다) 이번에 0.47.0 으로 맞췄다. **표의 과거 행은 손대지 않았다** — 그 행들이 적은 `NewMap`·`SameMap` 은 당시 실제로 확정돼 있던 내용이다. |
| 0.46.0 | 2026-09-01 | **접근 지표 절의 이름 통일 + AI 건물 배치 후보 판정의 전환 요구 등재 (코드 변경 0줄).** ① 「`MapDefinitionValidator` access metric」 절 도입부에서 **같은 절이 정의한 식별자를 다른 영문 어구로 부르던 표기 2곳**(성 쪽 1회 · 광산 덩어리 쪽 1회)을 정의된 이름 `StaticTraversable` 로 맞췄다. 같은 절의 인용 블록은 이미 그 이름을 쓰고 있어 **한 절 안에서 표기가 갈려** 있었다 — 0.45.0 의 ④·⑤·⑭ 와 같은 부류이며, 그때 우리말 표기만 정리하고 영문 어구 쪽이 남았던 자리다. ② 「기존 코드 전환 요구」 목록에 **`AIOpponentController.FindPlacementTile()` 의 배치 후보 판정을 이동 판정에서 「일반 건설」 조건으로 교체한다**는 항목을 추가했다(대상 파일 경로 · 현재 판정 위치 · **같은 파일의 XML 주석도 함께 옮긴다**는 조건을 함께 적었다). 🔴 **이것은 사양 변경이지만 오늘 `.cs` 는 한 줄도 바꾸지 않았다** — 새 조건이 읽어야 할 타일 상태 축이 아직 코드에 없어, `TileKind` 도입(= 무작위 맵 구현)과 **같은 시점에** 전환한다. **지금 등재해 두지 않으면 그때 이 파일이 누락된다**는 것이 이 항목의 존재 이유다. 기획 계약 쪽은 같은 회차에 `GameSystemRules/GameSystemRules_AI.md` 규칙 26 을 함께 고쳤다(후보 조건 교체 · 「후보가 거부돼도 탐색을 멈추지 않는다」 명시 · 전환 예정 표시). 이 어긋남은 **건설 불가 구역을 두는 맵 유형에서만 발현하며 그것은 5종 중 3종**이다(단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 3장과 규칙 9). 근거는 `_Tasks/2026-09-01/07_07_map-docs-out-of-scope-findings/`. **[같은 날 추가]** ③ **싱글플레이 맵 준비 「최초 경기」 실패 UI 의 팝업 타입을 모달 (Modal) 로 확정했다 — 이것은 표기 교정이 아니라 없던 사양을 새로 정한 결정이다.** 종전에는 `GameSystemRules/GameSystemRules_UI.md` 규칙 M-4 · 같은 문서의 반투명 배경 오버레이 모드 목록 · 이 문서 「복구 상태」 절 **어디에도 이 UI 의 타입 규정이 없었다**(2026-09-01 실측). 근거는 멀티 최초 실패를 모달로 정한 것과 같다 — **로딩 UI 가 이미 닫힌 뒤이고 `Retry`/`Lobby` 중 하나를 반드시 골라야 하는 자리**라, 배경 탭으로 닫히면 아무 선택지도 없는 화면에 남는다(타입별 동작 차이의 단일 소스는 `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 9). 🔴 **세 문서에 함께 반영했다** — 규칙 M-4 첫 불릿 · `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 16 실패 복구 절 · 이 문서 「복구 상태」 절. 멀티 최초 실패가 이미 그 세 곳 모두에 타입을 적고 있어, **한 곳만 적으면 같은 종류의 UI 인데 어떤 것은 세 곳 어떤 것은 한 곳이 되는 반쪽 상태**가 새로 생긴다. 이 항목도 **코드 변경 0줄**이다(맵 준비 실패 UI 는 무작위 맵 미구현으로 코드에 0건). **재경기 실패 쪽(멀티 `NewMap` · 싱글 재경기)은 팝업인지 결과 화면 그 자체인지 세 문서 모두 불명확해 추정하지 않고 그대로 두었다**(CLAUDE.md 규칙 10). **[같은 날 추가 2]** ④ **이 문서에서 바뀐 것은 「복구 상태」 절에 넣은 미정 항목 포인터 한 줄뿐이며, 복구 사양 본문은 한 줄도 바뀌지 않았다.** 같은 날 ③이 「그대로 두었다」고 적은 미결 항목들을 **대화가 아니라 문서에 못 박았는데**, 미정 표시 자체는 그 UI 들의 단일 소스인 `GameSystemRules/GameSystemRules_UI.md` 규칙 M-3·M-4 **한 곳에만** 달았다 — 세 문서에 같은 표시를 두면 그것이 또 사본이 되어 한 곳만 갱신되는 반쪽 상태를 새로 만든다. 다만 이 문서의 「복구 상태」 절만 읽는 사람에게는 **미정이라는 사실 자체가 닿지 않으므로**, 내용은 옮기지 않고 **어디를 보라는 포인터 한 줄만** 넣었다(같은 이유로 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 16 실패 복구 절에도 같은 포인터 한 줄을 넣었다). **[2026-09-01 정정]** 위 ③ 이 실패 복구 절의 소속을 「규칙 13」으로 적었으나 그 절은 규칙 16(멀티플레이 맵 전송과 실패 복구) 아래에 있고 규칙 13 은 「생성 완료 검증」이므로, 서술이 아니라 **잘못된 포인터**를 바로잡는 것이라 판단해 그 한 자리를 규칙 16 으로 정정했다(새 이력 항목은 만들지 않았다). |
<!-- docs-check: historical-section GameSystemRules_UI.md | 5~8 | 건물 배치 패널 UI -->
| 0.45.0 | 2026-08-31 | **무작위 맵 계약의 모호성 해소 + 맵 문서 전문 통독으로 찾은 정합 교정 (구현 착수 전 계약 교정, 코드 변경 0줄).** ① **봉쇄된 광산 덩어리 판정 신설** — 광산끼리 인접한 군집을 허용하므로 접근 칸이 하나도 없는 광산이 실제로 생길 수 있는데 그때의 판정이 없었다. **서로 인접한 광산을 「광산 덩어리」로 묶어 덩어리 단위로 접근 칸·접근 거리·도달 가능성을 판정**하고, **접근 칸이 하나도 없는 덩어리가 하나라도 있으면 판 전체를 버린다.** 근거는 채굴소가 인접 6타일 전부를 자기 팀 소유로 만들어 덩어리 안에서 연쇄 건설이 된다는 코드 실측(`BuildingPlacementUseCase.PlaceMiningPost`·`PlaceBuildingInternal`). 회전이 덩어리를 덩어리로 옮기므로 대응쌍 교차 등식과 중앙 단독 광산 등식은 그대로 성립한다. 보편 공정성 문서인 `GameSystemRules/GameSystemRules_Map.md` 규칙 4 에도 **덩어리 단위로 도착 칸을 잡는다는 원칙 한 줄과 단일 소스 포인터**를 병기했다 — 그 문서만 읽는 사람에게는 덩어리 판정이 도달하지 않아, 두 문서의 서술이 문자 그대로는 어긋나 보였기 때문이다(같은 줄의 접근 거리 단위 표기도 규칙 문서 용어집이 정한 정식 표현으로 맞췄다). ② 규칙 문서의 영문 용어를 우리말로 정리(`attempt` → 「시도」, `rejection sampling` 뜻 병기). **코드 계약 문서인 이 문서의 영문 식별자는 손대지 않았다.** ③ 로그 「생성 소요 시간」의 **측정 구간을 확정** — seed 확정 직후부터 최종 맵 확정(검증 통과 또는 폴백 확정)까지의 누적 경과 시간 하나만 밀리초로 기록하며, 전송·해시 비교·씬 로드는 제외한다(규칙 16 범위). ④ **회전 연산의 이름 분리** — 좌표 변환은 `RotateCoord(p)`, 타일 상태 변환은 `RotateState(state)` 로 나눴다. 종전에는 **한 이름**이 입력도 하는 일도 다른 두 연산을 가리켜 사람이 구분할 수 없었다(2026-08-26 교정의 원인과 같은 부류). 코드에는 아직 이 이름이 없어 문서만 교체했으며, 폐기한 옛 이름은 규칙 문서 7장 용어 정의 「180도 회전」의 `_Avoid_` 행 한 곳에만 남겼다. 이 문서 「`MapDefinitionValidator` access metric」 절에서 같은 좌표 회전을 축약 표기로 적던 자리도 좌표용 이름으로 통일했다 — 한 문서 안에서 같은 연산이 두 이름을 갖는 것은 이번 교정이 없애려던 것과 같은 모양이다. ⑤ **`Hexiege.Application` 과 `UnityEngine.Application` 의 이름 충돌을 아키텍처 절에 명문화** — 종전에는 `FileSink.cs` 헤더 주석에만 있어 문서를 읽는 사람에게 도달하지 않았다. **개명하지 않고 완전 수식으로 회피하는 현행 방식을 유지**하기로 결정(현재 정상 동작 중 · 개명은 135곳 이상을 건드리는 코드 리팩터)하고 후속 판단 대기 항목으로 남겼다. 실측(2026-08-31): 선언 58파일 · `using` 77곳 · `UnityEngine.Application.` 완전 수식 24곳/9파일. ⑥ **접근 거리 개념의 용어를 「칸」 계열로 통일** — 성 접근 칸 · 광산 덩어리 · 광산 덩어리 접근 칸 · 접근 거리 넷이다. 이 문서 「`MapDefinitionValidator` access metric」 절의 우리말 서술도 같은 이름으로 맞췄다(**영문 코드 식별자는 손대지 않았다**). 종전 표기는 이 맵 문서가 이미 타일을 「칸」이라 부르는 것과 어긋났고, 덩어리 판정 도입으로 이어지지 않은 칸들의 모음이 될 수 있어 정확하지도 않았다. 폐기한 표기는 규칙 문서 7장 용어 정의 「접근 거리」의 `_Avoid_` 행 한 곳에만 남겼다. ⑦ **폴백 경로에서 경기 선택값이 그대로 유지된다고 읽히던 서술 2곳을 시도 0~99 구간 한정으로 좁혔다**(「무작위 맵 시작 동기화」 도입부 · 「결정적 PRNG 및 독립 스트림 계약」). 폴백은 광산 수·시작 광산 방향·실제 초기 골드를 템플릿 값으로 대체하므로 종전 서술은 같은 문서 「deterministic fallback 정의」와 문자 그대로 어긋나 있었다. 상세는 그 절을 가리키게 했다. ⑧ **access metric 도입부의 판정 단위를 광산 하나 → 광산 덩어리로 맞췄다** — 같은 절 아래에 덩어리 규정이 이미 있는데 도입부만 옛 단위였다. ⑨ **`MiningPost` 판정식에 `TileKind != Blocked` 를 추가** — 바로 아래 산문이 막힌 타일의 채굴소 건설을 금지하는데 판정식이 막지 않아, 이 절의 목적(판정식을 그대로 코드로 옮긴다)에 어긋났다. ⑩ **타일 표기를 코드 계약 형식으로 통일** — 「`HexGridRenderer`와 입력 표현」 절의 접두 없는 표기를 `TileKind.Normal`·`TileKind.NoBuild`·`TileKind.Blocked` 로 바꿨다. ⑪ **「deterministic fallback 정의」의 유형별 최대 광산 수·초기 골드 값에 단일 소스 경고를 달았다** — 구현자가 손에 들고 있어야 하는 값이라 남기되, 값이 바뀌면 규칙 문서를 먼저 고치고 이 절을 맞춘다(「archetype generator 알고리즘」 절과 같은 방식). ⑫ **규칙 문서 인덱스 `GameSystemRules.md` 「맵 관련 작업」 절 3건** — 이 인덱스는 그동안 맵 문서 교정 대상에서 빠져 있었다. ⓐ 접근거리 개념을 구역을 뜻하는 낱말로 적던 표기를 규칙 문서 용어집이 정한 「칸」 계열로 맞추고 재는 단위가 광산 덩어리임을 병기했다. ⓑ 맵 유형 개수를 인덱스에 옮겨 적던 사본을 없애고 `GameSystemRules_RandomMap.md` 3장을 단일 소스로 가리키게 했다. ⓒ 폴백이 경기 선택값을 그대로 쓴다고 읽히던 서술을 유형별 고정 템플릿 폴백으로 바로잡고 교체 규정의 단일 소스를 규칙 12로 가리키게 했다 — 같은 취지의 서술을 고친 다섯 번째 자리이며, 인덱스라 상세 규정은 옮겨 적지 않고 포인터만 뒀다. ⑬ **접두 없는 타일 상태 표기를 5곳 전부 `TileKind.` 접두형으로 통일** — ⑩에서 한 절만 고치는 바람에 **굵은 소제목과 그 본문이 서로 다른 표기로 갈려** 있었다. 이번에 「중립 광산 canonical orbit sampling」 1곳, 「`HexGridRenderer`와 입력 표현」 2곳(소제목 포함), 「`GridInteractionUseCase` 클릭 판정 순서」 2곳을 맞췄다. **개수를 못 박은 지시가 남긴 갈라짐이라, 이런 통일 작업은 절 단위가 아니라 문서 단위로 훑는다.** ⑭ **「`MapDefinitionValidator` access metric」 절의 성 접근 칸 표기를 우리말 정식 용어로 통일** — 한 문장 안에서 앞은 새 용어, 뒤는 옛 영문 표기로 갈려 있었다. 그 옛 표기는 규칙 문서 7장 용어 정의 「접근 거리」의 `_Avoid_` 행이 이미 금지한 것과 같은 부류다(**영문 코드 식별자 `Castle`·`StaticTraversable` 등은 손대지 않았다**). ⑮ **`GameSystemRules/GameSystemRules_AI.md` 「채굴소(MiningPost) 배치 — 병행 트랙」에 전환 예정 표시를 달았다** — 그 절의 광산 타일 조회 API 표기는 **폐기된 것이 아니라 현재 코드 그대로이며(2026-09-01 `.cs` 실측: 도메인 타일 정의·부트스트랩 대입·렌더러 사용 모두 살아 있음) 지금은 정확하다.** 그래서 **표기를 바꾸지 않고**, 무작위 맵을 구현하면 `MineKind` 기반 조회로 옮겨 간다는 사실과 그 단일 소스(이 문서 「기존 코드 전환 요구」)만 병기했다. 미리 바꿨다면 문서가 현재 코드와 어긋났을 것이다. ⑯ **`GameSystemRules/GameSystemRules_Units.md` 규칙 45 의 유닛 상태 이름을 `PathBlocked` 로 나눴다** — 아군 건물에 길이 막힌 **유닛의 상태**가 **타일의 상태**와 구분되지 않는 이름이어서 읽는 사람이 둘을 가려낼 수 없었다. ④의 회전 연산 이름 분리와 같은 부류다. 코드에 이 낱말 단위의 식별자가 0건임을 `.cs` 전수 실측으로 확인한 뒤 개명했으므로 **코드와 어긋나지 않는다**(규칙 45 자체가 미구현이다). ⑰ **`GameSystemRules/GameSystemRules_UI.md` 를 전문 통독해 맵 계약 접점(맵 준비 실패 UI 규칙 M-1~M-4 · 「무작위 맵 타일 선택과 건설 패널」 규칙 5~8)을 대조한 결과 어긋난 곳은 없어 고치지 않았다.** 통독에서 새로 드러난 것은 별도 보고 항목으로 남겼다. ⑱ **앞 항목들의 개명·표기 통일이 닿지 않은 자리 3곳을 마저 맞췄다** — ⑯의 유닛 상태 개명이 기획서 `GameDesignDocument.md` 에는 반영되지 않아 두 문서가 다른 이름을 쓰던 자리를 우리말 서술 + 정식 이름의 단일 소스 포인터로 정리했고, ⑬의 접두 통일에서 빠져 있던 「archetype generator 알고리즘」 절 `OuterGenerator` 항목의 타일 상태 표기를 형제 생성기 3개와 같은 접두형으로 맞췄으며, ⑫ⓑ에서 없앤 맵 유형 개수 사본이 `GameSystemRules.md` 「파일 목록」 표에는 다른 절이라 그대로 남아 있어 개수를 적지 않고 규칙 문서 3장을 가리키는 형태로 바꿨다. |
| 0.45.0 참조 보정 | 2026-10-15 | 위 원문이 인용한 「무작위 맵 타일 선택과 건설 패널」은 당시 UI 문서의 하위 제목이다. 현행 원본은 [GameSystemRules_BuildingPanelsUI.md](GameSystemRules/GameSystemRules_BuildingPanelsUI.md#건물-배치-패널-ui) 「건물 배치 패널 UI」 규칙 5~8이다. 원문은 보존하며 바로 앞 주석은 기존 UI 안내의 섹션을 한 참조에만 보정한다. |
| 0.44.0 | 2026-08-26 | **무작위 맵 대칭 기준 교정 + 타일 상태 통합 (구현 착수 전 계약 교정, 코드 변경 0줄).** ① 대칭 기준을 **참 180도 회전**(큐브 `p → 2C - p`, offset 은 짝수 열 `21-row`·홀수 열 `20-row`)으로 확정하고 행열 반전 `(10-col, 20-row)` 을 폐기했다 — 그것은 회전이 아니라 헥스 인접성을 보존하지 못한다. `SymmetricMapBuilder.SetPair` 의 대응 좌표도 함께 교체. ② 회전 상대가 없는 **짝수 열 0행 6칸**을 영구 차단 지형으로 고정(231칸 중 플레이 225칸). **배열·canonical hash 범위는 231칸 그대로 두고**, 대칭 대응쌍 비교에서만 제외하되 「항상 막힌 타일」 고정값 검사로 대체한다. ③ 타일 상태 2필드를 **`TileKind` 3상태 단일 필드**(`Normal`/`NoBuild`/`Blocked`)로 통합 — 뜻이 없는 조합이 표현 가능해 의미가 같은 맵이 다른 해시를 가질 수 있었다. ④ archetype generator 4종의 대역 수치를 **높이 단계** 기준으로 재작성(장애물 개방형 행 3~9 · 협곡형/외곽형 단계 18~24 · 3갈래형·외곽형 덩어리 단계 21±L). ⑤ 폴백을 **유형별 고정 템플릿 5개**로 교체하고 시작 광산 좌우 변환을 폐기(제작 도구 보존·재생성 원본 보관이 부수 조건). **폴백 경로에서는 `StartingMineSide`·`NeutralMineCount`·`InitialGold`가 템플릿 값으로 대체되고 fallback builder의 입력은 `MapType` 하나뿐임을 명시했다** — 종전 서술은 경기 선택 값이 폴백 경로에서도 입력으로 유지된다고 적고 있어 템플릿 구조와 양립하지 않았다. ⑥ 「보호 corridor」를 **필수 통로**로 개명. ⑦ 전송 조각 크기 1KB·전체 한도 64KB에 **「⚠️ 근거 미확인 — 구현 시 NGO 실측으로 확정」** 표시. **⚠️ 과대 표기 금지: 공정성 수치는 파이썬 시뮬레이션이며 Unity 실기 검증이 아니다.** 근거·결정 이력은 `_Tasks/2026-08-26/04_35_map-rules-correction/`. |
| (병합 주석) | 2026-10-13 | **버전 번호 충돌 — 이 표는 두 갈래의 번호 체계가 한 표에 합쳐져 있다.** 이 브랜치(`claude/random-map-phase2-runtime`)는 0.43.2 다음을 0.44.0(2026-08-26) → 0.45.0 → 0.46.0(2026-09-01) → 0.47.0 … 0.62.0 으로 매겼고, `origin/main` 은 같은 세 행(2026-08-26 · 08-31 · 09-01)을 **0.52.0 · 0.53.0 · 0.54.0 으로 다시 매겨** 그 자리를 B3 계열 행(0.44.0~0.50.0, 아래쪽)에 비워 줬다. 병합에서 **본문이 같은 세 행은 이 브랜치 번호(0.44.0~0.46.0)로 한 번만 남겼다**(main 쪽 사본과 다른 곳은 번호 자체와, 2026-09-01 행 본문 안의 인용 한 자리 「0.45.0」 ↔ 「0.53.0」 뿐이었다). 그 결과 **같은 번호가 서로 다른 변경을 가리키는 자리가 있다** — 0.44.0~0.46.0(위 2026-08-26~09-01 행 ↔ 아래 2026-07-20~07-27 행) · 0.47.0~0.50.0(위 2026-09-14~09-16 행 ↔ 아래 2026-07-30~08-04 행) · 0.51.0(위 2026-09-21 행 ↔ 아래 2026-08-18 행) · 0.52.0~0.54.0(위 2026-09-21~09-22 행 ↔ main 쪽이 08-26~09-01 행에 매겼던 번호). **번호는 재배열하지 않았다** — 다른 문서가 이 브랜치 번호(예: PROJECT_STATUS 의 「TDD 0.60.0」 · 「0.47.0」)를 이미 인용하고 있어, 날짜와 변경 내용으로 구분해 읽는다. 번호 체계 정리는 사용자 결정 사항이다. |
| 0.51.1 | 2026-08-18 | B3 유한 재탐색 guard 구현 반영. 결정적 ordered `HexCoord` path signature, 유닛별 frame budget, 동일·A→B→A·invalid 및 서로 다른 path 무진전 8회 상한 fail-closed, 다음-frame 적용, 실제 진전 reset과 실패 유닛 callback 재진입 억제를 추가했다. Unity compile·self-validation PASS, Android Host 실기 회귀 대기이므로 B3는 FAIL / OPEN 유지. |
| 0.51.0 | 2026-08-18 | 최신 `main`의 연구·상태효과·스킬·MistShrine 기능을 B3 서버 권위 이동과 통합. Android Host에서 동일·순환 재탐색이 한 frame을 점유할 수 있는 진행성 결함을 확인해 B3를 FAIL / OPEN으로 재개방하고, 유닛별 frame budget·동일/순환 path fail-closed·다음 frame 재개·진전 reset을 필수 교정 계약으로 추가했다. |
| 0.43.2 | 2026-08-12 | **MistShrine 물안개 힐 구현 반영 — 아키텍처 설계 변경 없음, 상태 표기 정정.** 2026-08-10 시점의 "구현 미착수" 표기를 **코드·프리팹 구현 완료 / 에디터 싱글플레이 실기 검증 완료**로 정정하고, 신설된 구성 요소는 문서 상단 노트에 기록했다(레이어 경계·서버 권위 원칙은 기존 규칙을 그대로 따르며 새 패턴을 도입하지 않았다). **아직 완료가 아닌 것(과대 표기 금지): 멀티플레이 실기 미검증 — 건물 HP 동기화·클라이언트 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱은 멀티에서만 도는 경로이며 실행된 적이 없다 · 물안개 지속 VFX 미제작 · 사용 버튼 아이콘 미제작 · 밸런싱 수치 미확정.** 구현 계약에 규칙 8-1-a(활성 물안개 위상 정렬)가 신설되었다 — `GameSystemRules/GameSystemRules_Buildings.md`. |
| 0.43.1 | 2026-08-10 | 건물 타입 주석 오류 정정 — `AutoTower`×3종족의 Transcendence를 `MistShrine`으로 적어 온 것을 **`VineTower`**로 수정했다. MistShrine은 방어 타워가 아니라 공격하지 않는 별도 힐 건물이며 `AutoTower`(= 2)와 완전히 별개인 `HealShrine`(= 6) enum 값을 쓴다는 점을 주석에 명문화. 당시 MistShrine 물안개 지속 힐은 **기획 확정 / 구현 미착수** 상태였다. 아키텍처·구조 변경 없이 표기 정정만 수행. 규칙: `GameSystemRules/GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템. |
| 0.50.0 | 2026-08-04 | B3 연속 이동 구현 상태 반영. `UnitServerTrajectoryPlanner`·`UnitTrajectoryStep`·`UnitPathCheckpointTracker`와 공통 서버 commit seam을 A*·Chase·PendingRepath·PostCombatResume에 연결하고, 실제 trajectory 거리 기반 waypoint 소비·corridor sweep/fallback repath·candidate-position acquire·committed Facing 동기화를 기록했다. Unity self-validation과 사용자 Editor smoke는 PASS지만 새 Android 역할교대·25종·Legacy rollback 전까지 B3는 조건부 진행 상태다. 공격 방향·Impact/피해 시점은 미완료다. |
| 0.49.0 | 2026-08-04 | B3 경기 단위 ReducerAuthoritative writer의 현재 정확성 증거를 반영하되, 정상 경로의 선분 전환마다 발생하는 stop-turn-go 품질 결함으로 완료 판정을 재개방했다. 서버 권위·Simulation Root/Visual Root·10°/15°·single-writer 계약을 유지하는 deep Authoritative Locomotion Module과 Server Trajectory Planner, 순수/멀티플레이 검증 Seam을 정의했다. |
| 0.48.0 | 2026-08-03 | Tracer B2 서버 이동·SimulationFacing Shadow와 25종 Blue/Red 누적 멀티플레이 검증을 반영했다. 신규 reducer는 10°/15° 히스테리시스와 이동 scope를 서버에서만 판정하고 위치·회전·Animator·NetworkTransform·path를 쓰지 않는다. 6개 인정 세션의 오류 카운터와 Client reducer·Simulation Root write는 0이다. 실제 이동·SimulationFacing single-writer 전환은 B3 미완료다. |
| 0.47.0 | 2026-07-30 | Tracer B1 최종 게이트 반영. 50개 프리팹의 `Interpolate=true`, `PositionLerpSmoothing=false`와 서버 권위/canonical world-space 불변을 기록하고, Android 1대와 Unity Editor counterpart의 Host/Client 역할교대 root-pose 교차 검증 및 rollback을 PASS로 확정했다. B2 서버 이동·SimulationFacing Shadow, 공격 방향, Impact/피해 시점, result seam과 권위 전환은 미완료다. |
| 0.46.0 | 2026-07-27 | Unit ActionSequence Tracer B1 Simulation/Visual Root seam 반영. `NetworkUnit`의 클라이언트 Simulation Root 보정을 제거하고 `VisualRootProjector`·`PresentationPoseProvider`로 표현 pose를 분리했다. 멀티플레이 UnitFactory의 canonical world position 생성, presentation 소비자 전환, 신규·교체 프리팹 승인 규격을 기록했다. 50개 프리팹 migration과 재실행 NO-OP는 확인됐지만 Host/Client runtime smoke와 실제 rollback failure injection은 아직 미완료다. |
| 0.45.0 | 2026-07-22 | Unit ActionSequence A1 순수 Application 경계 구현 상태 반영. `UnitActionContracts`·stateful `UnitActionSequencer`와 self-validation PASS를 기록하고, 런타임 pose/result seam·피해·RPC·VFX 미연결 및 다음 A2 server-authoritative pose seam shadow를 명시했다. |
| 0.44.0 | 2026-07-20 | 멀티플레이 유닛 이동·공격 규칙 v2 목표 아키텍처 반영. 서버 권위 `UnitActionSnapshot` + `AttackImpactResult`, AttackSequenceId/HitIndex 상관관계, Simulation Root/Visual Root 분리, 늦은 참가·순서 역전·중복 처리와 Clean Architecture 배치를 정의했다. 클라이언트 이동 예측, 유닛 점유 경로 차단, 공격자 FIFO 피격 표현은 Legacy로 정정했다. **문서 설계 완료이며 런타임 구현은 미완료다.** |
| 0.43.0 | 2026-07-20 | `MapTestModeEnabled`를 초기 골드 전용 설정으로 확정. 정상 모드는 광산 수 표, 테스트 모드는 `TestStartingGold=5000`을 사용하며 멀티플레이에서는 Host 표식·실제 골드가 권위값이다. 표식(0/1)과 실제 골드를 canonical bytes·SHA-256·로그에 포함하고 NewMap 준비에도 동일 권한 경계를 적용하도록 명문화. |
| 0.42.0 | 2026-07-19 | reliable map transfer의 10초 timeout, incomplete 한정 동일 nonce/package 전체 1회 재전송과 두 번째 실패 종료, version/size/SHA/deserialize/semantic/disconnect 즉시 실패를 확정. 최초·NewMap·싱글 실패 복구 UI/state, idempotent Retry와 새 seed 재준비, 내부 정보 비공개를 명문화. |
| 0.41.0 | 2026-07-19 | canonical 180 orbit 기반 중립 광산 균등 sampling, 유형별 zone/lane 제약, spacing filter 없음과 repair 없는 attempt reject 확정. castle-neighbor→mine-neighbor multi-source BFS access metric, center/pair 교차·HexCoord 거리, mine 전후 corridor 폭과 start-to-end continuity validator를 명문화. |
| 0.40.0 | 2026-07-19 | ObstacleOpen/Canyon/Outer/ThreeLane archetype의 행·열 단위 생성 알고리즘과 균등 선택, exact projection, NoBuild/Blocked profile, mine lane 제약, 실패 시 repair 없는 attempt reject를 확정. |
| 0.39.0 | 2026-07-19 | GridInteractionUseCase 입력 우선순위와 Blocked/NoBuild UX 확정. building action·MineKind MiningPost를 우선하고, Blocked는 deselect+패널 닫기/무토스트·무선택 이벤트, 소유 NoBuild는 선택+BuildingNotAllowed 토스트, 중립·적 NoBuild는 선택만 수행하도록 명문화. |
| 0.38.0 | 2026-07-19 | 무작위 맵 범위의 형식 marker를 임시 MapVersion(int, 초기값 1)으로 확정. unknown map format deserialize 차단과 Host/Client mismatch 시 map prep 실패만 담당하며 matchmaking/app update/global connection compatibility는 별도 GameProtocolVersion 중요 작업 책임으로 분리. |
| 0.37.0 | 2026-07-19 | 전역 GameProtocolVersion/build compatibility 관리를 무작위 맵 범위에서 분리. matchmaking same-version filter, custom lobby pre-Relay 검사, NGO approval/rejection, reconnect 검증, update-required UX를 별도 중요 작업으로 이관. |
| 0.36.0 | 2026-07-19 | NoBuild renderer를 standard mesh/height/owner base color + 반투명 짙은 회색 diagonal hatch 3개 overlay로 확정하고 selection 병행 표시를 명문화. Blocked는 domain/231 MapDefinition/hash/대칭 검증에는 유지하되 HexGridRenderer가 standard tile mesh/collider를 만들지 않는 빈 공간으로 표시하며 배경 collider 경유 선택을 차단하고 obstacle asset은 presentation만 대체하도록 확정. |
| 0.35.0 | 2026-07-19 | 최초 archetype/fallback은 빈 `DecorationDefinition` 목록과 decoration draw 0회를 사용하도록 확정. MapDefinition/canonical codec/hash/builder/validator는 위치와 type/materialVariant/scaleStep/rotationStep 정수 ID 스키마 및 exact 180° 대응을 선지원하고, 에셋·테마 확정 뒤 독립 Decoration 스트림으로 추가해 Terrain/MinePlacement를 불변으로 유지하도록 명문화. |
| 0.34.0 | 2026-07-19 | generation/validation 초기 실행 모델을 loading UI 표시→1 frame yield→main-thread synchronous 처리→전송으로 확정. generator/validator는 Unity API 없는 pure C#, Unity object/render 적용은 Game scene main thread로 제한하고 생성 시간·attempt count를 기록하며 profiling에서 hitch 확인 시에만 background 이전을 검토하도록 명문화. |
| 0.33.0 | 2026-07-19 | 최초 경기/NewMap 공용 persistent `NetworkMapTransfer` 프로토콜 확정. Begin/Chunk/Ready 메시지, 1KB chunk·64KB payload 한도, 중복 무시·순서 독립 재조립·stale nonce 무시, 완전 조립 후 hash/deserialize/semantic 검증 및 성공 ACK 전 씬 전환 금지를 명문화. |
| 0.32.0 | 2026-07-19 | `MapSelection`에서 맵 유형·허용 중립 광산 수·시작 광산 방향(A/B 50:50)을 최초 1회 선택해 attempt/fallback까지 고정하고, 초기 골드는 count에서 파생하며 retry는 지형 세부·중립 광산 위치·장식만 변경하도록 확정. fallback A/B는 시작 광산 좌우 대응 변환으로 지원. NewMap은 매치 설정을 유지하고 새 seed로 맵 값만 재선택하며 이전 canonical hash와 같은 후보를 거부하도록 명문화. |
| 0.31.0 | 2026-07-19 | `SymmetricMapBuilder`를 11×21 맵의 단일 생성 변경 경계로 확정. `SetPair(c,r)`→`(10-c,20-r)` 동시 적용, `SetCenter(5,10)` 전용, 팀/장식 rotation 자동 대응, archetype raw 수정 금지와 독립 MapDefinitionValidator exact symmetry 재검증 원칙 명문화. |
| 0.30.0 | 2026-07-19 | 무작위 맵 fallback을 25개 완성 바이너리/에셋 대신 유형별 deterministic base template 5개 + 허용 광산 수별 prevalidated symmetric mine slots 조합으로 확정. PRNG draw 없음, 고정 장식/무장식, 일반 MapDefinitionValidator 전체 검증, 코드/순수 정의 기반 schema migration 불필요 원칙 명문화. |
| 0.29.0 | 2026-07-19 | `MapDefinition` 타일별 `InitialOwner` 제거 확정. 성/팀 시작 광산 위치와 공용 인접 6타일 확장 규칙을 단일 입력으로 사용하고, 런타임 구성·validator가 순수 `InitialMapStateEvaluator`를 재사용하며 점유 건물 타일 제외 후 팀별 즉시 일반 건설 가능 고유 타일 10개를 검증하도록 명문화. |
| 0.28.0 | 2026-07-19 | 재경기 `RematchMapMode`(`SameMap`/`NewMap`)와 MapDefinition 생명주기 확정. 기존 요청/수락/거절·Game 씬 재로드 유지, 다른 mode 동시 요청 시 서버 선접수 제안 우선, SameMap 재사용, NewMap 검증 후 원자 교체, 실패 시 기존 결과/정의 유지, 싱글 선택 및 로비/연결 종료 시 컨텍스트 폐기 명문화. |
| 0.27.0 | 2026-07-19 | `MapDefinition` 권위 전송 형식을 canonical binary + SHA-256으로 확정. 고정 필드 순서·고정폭 정수·명시 byte order, 231 row-major 타일과 성/광산/장식 정규 정렬, string/float/hash 필드 제외, package 구조 및 Client 길이/버전→SHA→deserialize→semantic validator→ACK 순서, JSON 진단 전용 원칙 명문화. |
| 0.26.0 | 2026-07-19 | 무작위 맵 생성의 64-bit root seed와 프로젝트 전용 고정 정수 PRNG 계약 확정. MapSelection/Terrain/MinePlacement/Decoration 및 Attempt-0~99 독립 스트림 파생, 서브시스템·재시도 draw 격리, schema/version+seed 재현성과 Host 최종 MapDefinition 권위 원칙 명문화. |
| 0.25.0 | 2026-07-19 | 무작위 맵용 `HexTile` 상태를 `TerrainKind`/`BuildRule`/`MineKind`/`HasBuilding`으로 분리하고 `IsWalkable`을 setter 없는 계산 결과로 전환하는 설계 확정. 일반 건설·MiningPost·점령 조건과 건물 철거 시 영구 지형/광산 보존 원칙 명문화. |
| 0.24.0 | 2026-07-19 | 무작위 맵 `MapDefinition` 정규 데이터 계약 확정. FlatTop 11×21의 231개 타일 row-major 완성본, 메타데이터·타일 상태·성/광산 배치·정수 장식 식별자·최종 해시를 정의하고, float 해시 배제·hash 필드 제외 정규 직렬화·성/광산 단일 소스 원칙을 명문화. |
| 0.23.0 | 2026-07-19 | FlatTop 11×21 무작위 맵의 멀티플레이 시작 동기화 설계 확정. 로비 로딩 상태에서 Host가 생성·검증·재시도·폴백의 유일한 권위자로 최종 맵 전체를 전달하고, 양측 데이터 해시가 같을 때만 전투 씬으로 이동한다. 실패 시 기존 로비를 유지하며, 전투 씬에서는 확정 데이터를 구성한 뒤 양측 로드 완료 후 시뮬레이션을 시작한다. |
| 0.22.0 | 2026-07-17 | 도끼병(BattleAxe) 휩쓸기형 AoE 구현 — 특수 유닛 5종 중 첫 구현. 특수 공격 전략 핸들러 구조(`ISpecialAttackBehavior` + `SpecialAttackContext` + `SpecialAttackRegistry` + `SweepAttackBehavior`, `Scripts/Application/Combat/`) 신설, `UnitCombatUseCase.ExecuteAttack` 피해 로직을 `ApplyDamageToVictim` 헬퍼로 추출 후 특수 공격 훅 1줄 추가. 휩쓸기 판정 = 월드 좌표 전방 부채꼴(reach/arc), 튜닝 SO `SpecialAttackConfig`(Infrastructure/Config) 신규. "특수 공격 전략 핸들러 구조" 서브섹션 추가. `GameSystemRules_Units.md` 규칙 23~27 등재. BattleAxe attackRange 0.5→0.75, 타격 1.1667s(클립 OnAttackHit 주입). |
| 0.21.0 | 2026-07-13 | 유닛 애니메이션 상태 동기화를 엣지 트리거 RPC(`StartWalkAnimationClientRpc` 등)에서 `NetworkUnit`의 NetworkVariable(`UnitAnimState` None/Walk/Attack) **레벨 동기화**로 전환. 클라 스폰 시 현재 값 자동 적용으로 스폰 레이스 유실 소멸, `UnitView.Initialize` 후 `ReapplyAnimStateToView` 멱등 재적용 봉합, 재경로 첫 스텝 역방향은 `AlignPathStartToTransform`로 보정. "유닛 애니메이션 상태 동기화" 서브섹션 신규 추가. 규칙 U-22 등재(규칙 21 상위 대체). |
| 0.20.0 | 2026-06-26 | `IUnitFactory` 인터페이스 도입(Bootstrap/Infrastructure 역방향 의존 제거 리팩토링). `IGameServices.GetUnitFactory()` 반환 타입을 구체 클래스 `UnitFactory`(Infrastructure) → `IUnitFactory`(Application) 인터페이스로 변경. "의존성 방향 추상화(Application 인터페이스 패턴)" 섹션 신규 추가(IGameServices/IUnitFactory/IEntityPositionProvider/IForfeitService 정리). 동작 변경 없음. |
| 0.19.0 | 2026-06-10 | AI 시나리오 ScriptableObject 3종족 개편. Domain/AI 레이어 신규(DifficultyLevel, BuildOrderStep, AIActionType). 종족별 단일 에셋 구조. |
| 0.18.0 | 2026-06-05 | Firebase 백엔드 전환 완료 반영 (Firebase SDK v13.11.0 + GPGS v2.1.0 설치 완료, PlayFab → Firebase/Firestore 구조로 교체). 폴더 구조 Bootstrap/Diagnostics 추가. ScriptableObject 기반 UnitStats/BuildingStats 반영. BuildingType 26종 확장 반영. ProductionState PendingQueue 구조 반영. OnEntityDied → OnUnitDied+OnBuildingDied 강타입 분리 반영. |
| 0.17.0 | 2026-03-19 | 유닛 스탯 코드 예시 재확정 (ATK: Pistoleer=6, Assault=1, Sniper=10 / 사거리: 1.0/2.0/5.0 / epsilon 0.05f 명시 / Sniper MoveSpeed=0.25). Castle HP 50→100. A* 목표 타일 blocked 체크 제거 반영. UIAnimator/AnimatedPanel 패턴 섹션 추가. 개발 로드맵 섹션 삭제 (ROADMAP.md 참조로 대체). |
| 0.16.0 | 2026-03-19 | 카메라 줌 DOTween 보간 완료 (CameraController _targetZoom + DOTween.To Ease.OutCubic, _zoomDuration SerializeField). |
| 0.15.0 | 2026-03-17~18 | 전역 로딩 스크린 (LoadingScreen.cs 싱글턴, Show/Hide DOFade, sceneLoaded 자동 Hide). 재경기 시스템 (RematchRequestPopup, RequestRematchServerRpc, NGO LoadScene 재로드). 멀티플레이 로비 복귀 버그 수정 (로컬 독립 처리, WaitForSecondsRealtime 기반 30초 카운트다운). |
| 0.14.0 | 2026-03-14~16 | 팀별 프리팹 코드 연동 (UnitFactory/BuildingFactory 팀+타입별 분기). Assault/Sniper UnitType enum + UnitStats + UnitProductionStats. 공격 애니메이션-타격 동기화 (AnimationEventRelay + Animation Event + scale punch). 유닛 메시 방향 보정 (하위 Mesh Y=30°). 유닛 회전 DOTween 보간 (ApplyDirection/PlayAttackAnimation). 랜덤매칭 씬 전환 버그 수정 (GetStableHash polynomial hash). |
| 0.13.0 | 2026-03-09 | GameConfig AnimationFps 필드 제거 (미사용), Walk 애니메이션 연속 재생 수정 (매 스텝 0f 리셋 → 이미 Walk 상태이면 리셋 안 함), UnitStats HP 50으로 현행화, SetDependencyReferences 시그니처 현행화 (animData 제거, unitFactory/buildingFactory 추가), T키 자동이동 섹션 제거 (기능 삭제됨), 랠리마커 sortingOrder 제거 (3D Z-buffer 전환 완료) |
| 0.12.0 | 2026-03-07 | 3D 전환 반영: Netcode 버전 2.9.2, 애니메이션 Animator(Mecanim) 기반(Walk/Attack/Dead), sortingOrder 폐기→Z-buffer 렌더링, TileHeight 0.866 통일, ViewConverter 시스템 문서화, 비주얼/카메라 스타일 3D 이소메트릭 반영 |
| 0.11.0 | 2026-02-20 | HUD 타일 카운트: GameHudUI에 블루/레드 팀 보유 타일 수 표시 추가(_blueTileCountText/_redTileCountText), PopulationUseCase.GetMaxPopulation() 활용. 게임 종료 UI 버그 수정: GameEndUI를 Awake() 자체 구독→Initialize() 패턴으로 변경(비활성 패널에서 Awake 미호출 문제 해결), GameBootstrapper.LoadMap()에서 Initialize() 호출, 재시작 시 구독 정리/재구독 처리 |
| 0.10.0 | 2026-02-15 | 공성 시스템: ProductionTicker 공성 흐름(랠리→Castle→siege 전진), UnitView.OnMoveComplete 콜백, 공성 목록 관리(등록/제거), TickSiege 1초 간격 전진 체크. PopupClosedFrame 패턴: BuildingPlacementUI/ProductionPanelUI ClosedFrame으로 팝업 닫힘 같은 프레임 클릭 통과 방지 |
| 0.9.0 | 2026-02-15 | 랠리포인트 시스템 개선: 마커 표시(3초 자동 숨김 + 팝업 연동), RallyPointChangedEvent 이벤트 추가, BFS 빈 타일 탐색(maxRange=3), 배럭 타일 설정→해제, ProductionTicker 마커 관리, ProductionPanelUI 마커 표시/숨김 연동, GameConfig.RallyPointPrefab 추가, 팝업 설정 후 자동 닫힘 |
| 0.8.1 | 2026-02-14 | Per-step 타일 가용성 체크 추가: UnitMovementUseCase.IsTileBlockedBySameTeam() 메서드 추가, MoveAlongPath 각 스텝 시작 전 같은 팀 차단 검증, 차단 시 현재 위치→최종 목적지 재탐색(RequestMove), 재탐색 실패 시 이동 중단. 전투 흐름 다이어그램에 per-step 체크 단계 추가 |
| 0.8.0 | 2026-02-14 | 유닛 이동/전투 시스템 개선: ClaimedTile(같은 팀 이동 중 타일 선점, 적 팀 투과), 이동 중 거리 기반 전투(Lerp 중 매 프레임 사거리 체크), 타일 중앙 도착=전투 승리=점령 규칙 확립, UnitData.ClaimedTile 필드 추가, UnitMovementUseCase 차단 목록에 같은 팀 ClaimedTile 포함, UnitView.MoveAlongPath Claim 설정/해제 및 Lerp 중 전투 |
| 0.7.0 | 2026-02-14 | 유닛 생산 시스템: UnitProductionUseCase/ResourceUseCase/PopulationUseCase 추가, ProductionState/UnitProductionStats(Domain), ProductionTicker/ProductionPanelUI(Presentation), GameConfig 경제 설정, UnitFactory 런타임 의존성 주입(SetDependencyReferences), 영토 확장(건물 건설 시 인접 타일 점령), 경로탐색 아군/적군 무관 차단, 유닛 스폰 점유 검증, 생산 이벤트 4종 추가 |
| 0.6.0 | 2026-02-13 | 전투 시스템 고도화: IDamageable 인터페이스 도입(유닛/건물 통합 전투), BuildingStats/UnitStats 중앙 스탯 관리, 이벤트 일반화(EntityAttacked/EntityDied), 경로탐색 적 유닛 우회(blockedCoords), 이동 중 전투(매 타일 공격 체크 + 전투 후 이동 계속), 사망 엔티티 데이터 정리(Dictionary 제거 + 타일 복구), T키 자동/수동 이동 토글(양팀 Castle 방향 자동 이동) |
| 0.5.0 | 2026-02-08 | 건물 배치 시스템(MVP Phase 1) 추가: BuildingType/BuildingData, 배치 흐름(자동/수동), 정렬 순서(건물 50), BuildingPlacedEvent |
| 0.4.0 | 2026-02-08 | 타일 선택 하이라이트 버그 수정 문서화: HexTileView 토글→결정적 할당, 선택 해제 이벤트 처리 설명 추가 |
| 0.3.0 | 2026-02-08 | 듀얼 Orientation: OrientationConfig, PointyTop(7×17)/FlatTop(10×29), 런타임 맵 전환(LoadMap), HexCoord/A* 코드 현행화 |
| 0.2.0 | 2026-02-07 | 전투 시스템 추가: UnitData 전투 스탯, UnitCombatUseCase 전투 흐름, 이벤트 기반 통신 (Attack/Died) |
| 0.1.0 | 2026-01-27 | 초기 문서 작성 |

---

**문서 끝**
