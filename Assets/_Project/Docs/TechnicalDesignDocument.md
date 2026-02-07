# Hexiege - 기술 설계서 (Technical Design Document)

**버전:** 0.4.0
**최종 수정일:** 2026-02-08
**작성자:** HANYONGHEE

---

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

## 🛠️ 기술 스택

### 핵심 기술
| 항목 | 기술 | 버전 |
|------|------|------|
| **게임 엔진** | Unity | 6000.0.x (Unity 6 LTS) |
| **렌더 파이프라인** | URP | Universal Render Pipeline |
| **네트워크** | Netcode for GameObjects | 2.1.0+ |
| **전송 레이어** | Unity Transport (UTP) | - |
| **NAT 관통** | Unity Relay | - |
| **매칭** | Unity Lobby | - |
| **인증** | Unity Authentication | - |
| **경로찾기** | A* Pathfinding Project | Free/Pro |
| **백엔드** | PlayFab | - |
| **이벤트 시스템** | UniRx | 7.1.0 |
| **애니메이션** | DOTween | 1.2.765 |
| **모바일 입력** | Lean Touch+ / Unity Input System | - |

### 개발 언어
- **C# 9.0** (Unity 2022.3+)
- **JavaScript** (PlayFab CloudScript)

### 개발 도구
- **IDE**: Visual Studio 2022 / Rider
- **버전 관리**: Git + GitHub
- **빌드**: Unity Cloud Build (선택)
- **분석**: Firebase Analytics (선택)

---

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
    │   ├── Domain/              # 순수 C# 엔티티
    │   ├── Application/         # Use Cases
    │   ├── Infrastructure/      # 외부 연동
    │   ├── Presentation/        # Unity UI/View
    │   └── Core/                # 공통 유틸리티
    ├── Prefabs/
    ├── Materials/
    ├── Scenes/
    └── Resources/
```

---

## 🌐 네트워크 설계

### Netcode for GameObjects

#### 아키텍처
```
클라이언트 A ←→ Unity Relay ←→ 클라이언트 B
                (NAT 관통)
```

**특징**:
- P2P 방식 (Host-Client 모델)
- Host가 서버 역할 (Authoritative)
- Unity Relay로 NAT 관통 자동 처리

#### 동기화 전략

**NetworkVariable (자동 동기화)**:
```csharp
// 자원 (서버 → 클라이언트)
NetworkVariable<int> resources = new NetworkVariable<int>(
    value: 1000,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);
```

**ServerRpc (클라이언트 → 서버)**:
```csharp
[ServerRpc(RequireOwnership = false)]
void SpawnUnitServerRpc(UnitType type, Vector3 position) {
    // 서버에서 검증 + 실행
}
```

**ClientRpc (서버 → 클라이언트)**:
```csharp
[ClientRpc]
void ShowEffectClientRpc(Vector3 position) {
    // 모든 클라이언트에 이펙트 표시
}
```

#### 동기화 대상
| 데이터 | 동기화 방식 | 빈도 |
|--------|------------|------|
| **건물 건설** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **유닛 생성** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **타일 점령** | NetworkList<TileOwnership> | 변경 시 |
| **자원** | NetworkVariable<int> | 변경 시 |
| **본기지 체력** | NetworkVariable<int> | 변경 시 |
| **유닛 이동** | 클라이언트 예측 (AI 동일 로직) | - |

#### 치팅 방어
- **서버 검증**: 모든 행동을 서버(Host)에서 검증
- **자원 관리**: 클라이언트는 읽기만 가능
- **유닛 생성**: 인구수/자원 서버 체크
- **타일 점령**: 유닛 위치 서버 관리

---

## 🗄️ 백엔드 설계

### PlayFab 구조

```
Unity 클라이언트
    ↓
PlayFab Client SDK
    ↓
PlayFab Services
    ├─ Authentication      (로그인)
    ├─ Player Data         (유저 데이터)
    ├─ Virtual Currency    (골드, 크리스탈)
    ├─ Inventory           (아이템)
    ├─ Leaderboard         (랭킹)
    ├─ Matchmaking         (매칭)
    ├─ CloudScript         (서버 로직)
    └─ Economy             (인앱 결제)
```

### CloudScript 함수 목록

| 함수명 | 역할 | 호출 시점 |
|--------|------|-----------|
| **ClaimDailyReward** | 일일 보상 지급 | 로그인 시 |
| **PurchaseItem** | 상점 아이템 구매 | 구매 버튼 |
| **CompleteMatch** | 경기 종료 처리 | 경기 끝 |
| **UpdateLeaderboard** | 랭크 점수 갱신 | 경기 끝 |
| **GrantBattlepassReward** | 배틀패스 보상 | 티어 달성 |

### 주요 API 호출 예시

**로그인**:
```csharp
PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest {
    CustomId = SystemInfo.deviceUniqueIdentifier,
    CreateAccount = true
}, result => {
    Debug.Log("Logged in: " + result.PlayFabId);
}, error => {});
```

**아이템 구매 (CloudScript)**:
```csharp
PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest {
    FunctionName = "PurchaseItem",
    FunctionParameter = new { itemId = "skin_human_future", price = 299 }
}, result => {
    var response = JsonUtility.FromJson<PurchaseResult>(result.FunctionResult.ToString());
}, error => {});
```

---

## 💾 데이터베이스 스키마

### PlayFab 데이터 구조

#### User Data
```json
{
  "userId": "ABC123",
  "displayName": "한용희",
  "level": 15,
  "exp": 2500,
  "currency": {
    "gold": 5000,
    "crystal": 250
  },
  "stats": {
    "totalGames": 120,
    "wins": 65,
    "losses": 55,
    "winRate": 0.54,
    "rankPoints": 1450
  },
  "inventory": {
    "races": ["human", "elemental"],
    "skins": ["human_future", "elem_dark"],
    "emotes": ["gg", "nice", "oops"]
  },
  "battlepass": {
    "tier": 25,
    "exp": 12500,
    "isPremium": true
  }
}
```

#### Match Data
```json
{
  "matchId": "match_20260127_001",
  "mode": "ranked",
  "duration": 635,
  "players": {
    "blue": { "userId": "user_A", "race": "human" },
    "red": { "userId": "user_B", "race": "elemental" }
  },
  "result": {
    "winner": "blue",
    "blueStats": { "tilesControlled": 48, "unitsKilled": 35 },
    "redStats": { "tilesControlled": 32, "unitsKilled": 28 }
  }
}
```

---

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

---

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
| TileHeight | 0.82 | 0.36 |
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
List<HexCoord> path = HexPathfinder.FindPath(grid, start, goal);
```

---

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

#### 유닛 전투 스탯
```csharp
public class UnitData {
    public int MaxHp { get; }          // 최대 체력 (기본: 10)
    public int Hp { get; set; }        // 현재 체력
    public int AttackPower { get; }    // 공격력 (기본: 3)
    public int AttackRange { get; }    // 사거리 (기본: 1, 인접 타일)
    public bool IsAlive => Hp > 0;
}
```

#### 전투 흐름
```
유닛 이동 명령 (InputHandler)
  ↓
A* 경로 이동 (UnitView 코루틴)
  ↓
이동 완료
  ↓
인접 6타일에서 적 탐색 (UnitCombatUseCase.TryAttack)
  ↓ 적 발견
공격 방향 계산 → 데미지 적용 (target.Hp -= AttackPower)
  ↓
공격 이벤트 발행 → Attack 애니메이션 재생
  ↓
적 HP ≤ 0? → 사망 이벤트 발행 → GameObject 파괴
  ↓
사거리 내 적이 남아있으면 반복 공격
  ↓
적 없음 → Idle 상태 복귀
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

#### 이벤트 기반 전투 통신
```csharp
// 공격 이벤트 (UnitCombatUseCase → UnitView)
GameEvents.OnUnitAttack.OnNext(new UnitAttackEvent(
    attackerId, targetId, damage, direction));

// 사망 이벤트 (UnitCombatUseCase → UnitView)
GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unitId));
```

---

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

---

## 📊 개발 로드맵

### Phase 1: 코어 메커니즘 (3~4주)
- 육각형 그리드 생성
- 타일 점령 시스템
- 기본 생산 시스템

### Phase 2: 네트워크 (2~3주)
- Netcode 통합
- Relay 연결
- 동기화 테스트

### Phase 3: 게임플레이 (3~4주)
- 5가지 건물
- 3종족 유닛
- AI 시스템

### Phase 4: 백엔드 (2~3주)
- PlayFab 연동
- 계정 시스템
- 인앱 결제

### Phase 5: 컨텐츠 (3~4주)
- UI/UX
- 튜토리얼
- 밸런싱

### Phase 6: 출시 (2주)
- QA 테스트
- 최적화
- 스토어 등록

**총 개발 기간**: 약 4개월

---

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 0.4.0 | 2026-02-08 | 타일 선택 하이라이트 버그 수정 문서화: HexTileView 토글→결정적 할당, 선택 해제 이벤트 처리 설명 추가 |
| 0.3.0 | 2026-02-08 | 듀얼 Orientation: OrientationConfig, PointyTop(7×17)/FlatTop(10×29), 런타임 맵 전환(LoadMap), HexCoord/A* 코드 현행화 |
| 0.2.0 | 2026-02-07 | 전투 시스템 추가: UnitData 전투 스탯, UnitCombatUseCase 전투 흐름, 이벤트 기반 통신 (Attack/Died) |
| 0.1.0 | 2026-01-27 | 초기 문서 작성 |

---

**문서 끝**
