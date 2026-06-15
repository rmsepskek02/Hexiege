# Hexiege - 기술 설계서 (Technical Design Document)

**버전:** 0.19.0
**최종 수정일:** 2026-06-10
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
    │   ├── Application/         # Use Cases, GameEvents, NetworkContext
    │   ├── Infrastructure/      # 외부 연동, Network Controllers, Factories
    │   ├── Presentation/        # Unity UI/View, UnitView, CameraController
    │   ├── Core/                # 공통 유틸리티 (HexMetrics, ViewConverter)
    │   └── Diagnostics/         # 진단/디버그 전용 도구
    ├── Prefabs/
    ├── Materials/
    ├── Scenes/
    └── Resources/
```

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

### 9. PopupClosedFrame (팝업 닫힘 프레임 보호)

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
- UnitSpawnUseCase.SpawnUnit()에서 타일 IsWalkable 검증 + 유닛 점유 검증 (GetUnitAt)
- 건물이 있거나 다른 유닛이 이미 있는 타일에는 유닛 생성 불가

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
2. BuildingPlacementUseCase.RemoveBuilding() → Dictionary 제거 + 타일 IsWalkable 복구
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

### 건물 배치 시스템 (MVP Phase 1)

프로토타입 완료 후 첫 MVP 기능. 건물 배치 + 시각화만 구현 (자원/생산 시스템 미포함).

#### 건물 타입 (2026-05-17 26종 확장)
```csharp
// BuildingType enum — 26종
// Castle×3종족, MiningPost×3종족
// 종족별 생산라인 3단계: HumanBarracks1/2/3, AncientGrove1/2/3, PrimalSanctuary1/2/3
// AutoTower×3종족 (Human=CannonTower, Spirit=MagicTower, Trans=MistShrine)
// 추가: ResearchLab (미구현)
//
// BuildingTypeHelper.cs (Domain 레이어):
//   IsProductionBuilding(type) — 생산 패널 표시 여부
//   GetStage(type)             — 건물 단계 (1/2/3)
//   GetNextStage(type)         — 업그레이드 대상 타입
//   CanUpgrade(type)           — 업그레이드 가능 여부
//   CanShowActionPanel(type)   — 비생산 건물 액션 패널 표시 여부
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
    -   해당 타일의 상태를 '건설됨'으로 변경합니다 (`HexTile.IsWalkable = false`).
    -   `GameEvents.OnBuildingPlaced` 이벤트를 발행(OnNext)하여 시스템의 다른 부분에 건물 배치가 완료되었음을 알립니다.
    -   건물 파괴 시: `RemoveBuilding(id)` → Dictionary 제거 + `HexTile.IsWalkable = true` 복구.

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
int StartingGold = 500;           // 게임 시작 골드
float MiningGoldPerSecond = 10f;  // 채굴소 초당 수입
int BarracksCost = 100;           // 배럭 건설 비용
int MiningPostCost = 50;          // 채굴소 건설 비용
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


> 개발 진행 현황 및 로드맵은 `ROADMAP.md`, `PROJECT_STATUS.md` 참조.

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
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
