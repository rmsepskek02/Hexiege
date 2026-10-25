# Hexiege - 기술 설계서 (Technical Design Document)

**문서 역할**
- **목적·질문:** 공통 아키텍처·구현 방법과 개발 도구 사용법을 설명한다.
- **포함:** 계층·데이터·공통 구현 패턴·개발 환경 및 그 설계 이유.
- **제외·담당:** 제품 동작은 [규칙](GameSystemRules.md), 수치는 [스탯](StatsReference.md), 현재 상태는 [현황](PROJECT_STATUS.md), 날짜별 실행은 [이력](History/ImplementationVerificationHistory.md).
- **원본·참조:** 공통 기술 설계 원본. 네트워크 상세 기술은 [네트워크 TDD](TechnicalDesignDocument_Network.md).
- **갱신:** 실제 구조·기술 계약·도구 절차가 바뀌면 해당 절을 갱신한다. compile·manifest·빌드 시도는 근거로 연결한다.

날짜별 구현·native·빌드 원문은 [보존 이력](History/ImplementationVerificationHistory.md#tdd-handoff), 개별 연결·수용은 [Matrix](Assets/UnitCombatAssetMatrix.md)·[VFXSFXList](Assets/VFXSFXList.md)를 따른다. native PASS·빌드 시도·실기 수용·전체 이행은 별도 축이다.
**버전:** 0.62.0
**최종 수정일:** 2026-10-13
**작성자:** HANYONGHEE

MistShrine은 독립 `MistShrineUseCase`의 인스턴스별 누적기·대상 재수집과 전용 네트워크 건물 힐 경로를 사용해 유닛 HoT/DoT 채널과 분리한다. 현재 구현·검증 범위는 [Status](PROJECT_STATUS.md), 당시 구성·검증 기록은 [원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-mist-01)을 따른다.

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
| **게임 엔진** | Unity | 6000.3.5f2 |
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
│  (UseCase·계약)                     │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│         Domain Layer                │  ← Entities, Value Objects
│  (순수 C#, Unity 독립)              │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│    Infrastructure Layer             │  ← Network, DB, External APIs
│  (Netcode, Firebase, 외부 연동)     │
└─────────────────────────────────────┘
```

### 폴더 구조
```
Assets/
└── _Project/
    ├── Scripts/
    │   ├── Bootstrap/           # Composition Root (GameBootstrapper partial × 4, LoginBootstrapper)
    │   │   └── Debug/           # 에디터 전용 맵 준비·투영 강제 실패 플래그
    │   ├── Domain/              # 순수 C# 엔티티
    │   ├── Application/         # Use Cases, GameEvents, NetworkContext, Interfaces
    │   ├── Infrastructure/      # 외부 연동, Network Controllers, Factories
    │   ├── Presentation/        # Unity UI/View, UnitView, CameraController
    │   ├── Core/                # 공통 유틸리티 (HexMetrics, ViewConverter)
    │   └── Debug/               # UIManager 테스트 버튼 핸들러
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

완전 수식으로 충돌을 회피하며 Application 레이어 전체의 개명은 별도 코드 리팩터다. 이름이 좌표 변환과 상태 변환처럼 서로 다른 대상을 가리키지 않도록 새 API의 의미를 구분한다. 당시 규모·개명 보류 판단은 [이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-namespace)에 보존한다.

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

<a id="네트워크-동작-규정은-별도-문서로-이관됐다--최상위-원칙--이탈-시-승패-기록--인게임-이탈-처리-2026-10-12"></a>
<a id="netcode-for-gameobjects"></a>
<a id="아키텍처"></a>
<a id="동기화-전략"></a>
<a id="동기화-대상"></a>
<a id="무작위-맵-시작-동기화-확정-설계-13단계-구현--잔여-있음"></a>
<a id="mapdefinition-정규-데이터-계약"></a>
<a id="생성검증-실행-모델"></a>
<a id="결정적-prng-및-독립-스트림-계약"></a>
<a id="symmetricmapbuilder-생성-경계"></a>
<a id="archetype-generator-알고리즘"></a>
<a id="중립-광산-canonical-orbit-sampling"></a>
<a id="mapdefinitionvalidator-access-metric"></a>
<a id="필수-통로-validator"></a>
<a id="deterministic-fallback-정의"></a>
<a id="hextile-런타임-상태-계약"></a>
<a id="재경기-mapdefinition-생명주기"></a>
<a id="결과-화면-이탈-판정통보-구조-단계-6-구현--관측-범위별-검증"></a>
<a id="치팅-방어"></a>
<a id="결과-화면-이탈-판정통보-구조-2026-09-16-확정-설계--미구현"></a>

네트워크 데이터·동기화·맵 생성/전송·재경기 생명주기·이탈 판정 구조는 [네트워크 TDD](TechnicalDesignDocument_Network.md)가 원본이다. 제품 동작·이탈 시 승패는 [네트워크 규칙](GameSystemRules/GameSystemRules_Network.md), 경기 준비·복구는 [경기 운영 규칙](GameSystemRules/GameSystemRules_MatchFlow.md), 화면은 [경기 흐름 UI](GameSystemRules/GameSystemRules_MatchFlowUI.md)를 따른다.

기존 세부 제목은 호환 앵커로만 유지하며 같은 안내 문장을 절마다 반복하지 않는다. [종전 안내 구조](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-network-aliases).
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

<a id="로그인-흐름-authsystemrulesmd-참조"></a>

### 로그인 구현 경계

익명·Google Play Games·이메일 인증은 Infrastructure의 `FirebaseAuthService`, 익명→실계정 연동은 Application의 `AccountLinkUseCase`가 담당한다. LoginUseCase가 인증 결과에 따라 UGS 브릿지를 호출하며, UI 전환·인증/닉네임 선행 조건은 [인증 규칙](AuthSystemRules.md)이 원본이다. SDK·Console 설정·실기 완료 여부는 [현재 상태](PROJECT_STATUS.md)에서 확인하고 이 구조 설명에 별도 상태표를 두지 않는다. [이전 코드/설정 상태표](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-auth-status).

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

아래 코드는 책임 분리와 호출 형태를 설명하는 개념 예시다. 현재 실제 클래스의 전체 API·멤버나 기능 완료 상태를 보증하지 않으며, 구체 구현은 코드와 해당 시스템 원본 문서를 함께 확인한다.

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

<a id="듀얼-orientation-지원"></a>

### Orientation과 대전 맵

그리드 좌표 계산과 대전 맵 선택은 구분한다. 현재 대전 맵은 **FlatTop 11×21**이며 [무작위 맵 규칙](GameSystemRules/GameSystemRules_RandomMap.md)의 좌표·대칭 계약과 [네트워크 TDD](TechnicalDesignDocument_Network.md)의 MapDefinition으로 구성한다. 기존 PointyTop/FlatTop 설정 예시는 예전 그리드 설명이며 현재 대전 크기·선택 UI가 아니다. PointyTop 실제 도입은 별도 설계·검증 범위다. [이전 설정 예시](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-orientation).

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

<a id="a-경로찾기-커스텀-구현"></a>

### A* 경로찾기와 이동 점유

`HexPathfinder`는 헥스 인접 관계와 정적 이동 가능 여부·건물 차단을 사용한다. 다른 유닛의 현재 좌표·ClaimedTile은 A* 차단 목록으로 사용하지 않으며 유닛 겹침을 허용한다. 이동 중 다음 구간에 동적 건물이 생기면 현재 권위 위치에서 원래 목표로 재경로한다. 우회가 없는 경우의 적 건물 공격·아군 PathBlocked는 [유닛 규칙 45](GameSystemRules/GameSystemRules_Units.md#유닛-이동-시스템)를 따른다.

`ClaimedTile`과 타일 도착·점령 정보는 경로 차단을 위한 아군 예약과 구분한다. 스폰 시 건물·유닛 점유 검사는 별도의 생성 조건이며, 겹침을 허용하는 이동 계약을 스폰 예외로 확대하지 않는다. 자세한 이동 writer·trajectory/checkpoint는 아래 서버 권위 구조, 타일의 계산된 walkability는 [HexTile 계약](TechnicalDesignDocument_Network.md#hextile-런타임-상태-계약)을 따른다. [과거 점유·선점 차단 구현](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-pathfinding).

---

<a id="ai-시스템"></a>

## 🤖 AI 시스템

<a id="유닛-ai-상태머신-mvp-목표"></a>
<a id="ai-스크립트-구조-mvp-목표"></a>

### 유닛 행동 구조

유닛 행동은 서버 권위 이동·전투 책임과 표현을 구분한다. 순수 상태머신 예제의 UnitAI가 현재 생산 유닛의 단일 구현이라고 보지 않는다. 실제 이동의 공통 commit seam·planner와 SOLO 일반 공격 occurrence clock·별도 HealNow는 아래 현행 구조를 따른다. [초기 MVP 상태머신·코드 예시](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-unitai-mvp).

<a id="현재-구현-전투-시스템-프로토타입"></a>

### 전투 시스템의 책임

공통 피해·사망 데이터와 이동/공격 회차, 서버 권위 writer, Client 표현을 분리한다. 아래 인터페이스·데이터 예시는 경계를 설명하며 실제 전체 멤버 목록은 코드가 원본이다. 초기 코루틴 프로토타입을 현재 모든 행동의 구현으로 간주하지 않는다. [이전 구현 단계 설명](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-prototype-label).

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

<a id="중앙-집중-스탯-관리--scriptableobject-기반-2026-04-25-전환"></a>

#### 중앙 집중 스탯 관리 — ScriptableObject

`UnitStats`는 `UnitStatsConfig.asset`을, `BuildingStats`는 `BuildingStatsConfig.asset`을 읽는 조회 경계다. 건물 수치는 BuildingType과 Race 조합으로 구분하며 실제 저장된 Inspector 값이 코드 기본값보다 우선한다. 유닛의 생산 시간·골드·인구는 `UnitProductionStats`를 통해 같은 설정에 접근한다.

현재 종족별 roster와 값·단위는 [StatsReference](StatsReference.md), 공격 offset·주기는 UnitFactory의 실제 선택 클립/marker 소비 경로를 함께 대조한다. 저장 fallback을 항상 생산값으로 간주하지 않는다. [초기 API·타입 수 예시](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-stats-api).

#### 전투 스탯

**유닛 (UnitData)**:
```csharp
public class UnitData : IDamageable {
    public int MaxHp { get; }          // UnitStats에서 결정
    public int Hp { get; private set; }
    public int AttackPower { get; }    // UnitStats에서 결정
    public int AttackRange { get; }    // UnitStats에서 결정
    public bool IsAlive => Hp > 0;
    public HexCoord? ClaimedTile { get; set; } // 이동 구간 정보; 유닛 간 A* 차단 근거가 아님
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

<a id="전투-흐름-이동-중-거리-기반-전투"></a>

#### 전투·이동·점령의 연결

1. 이동 목표와 경로를 정하고 서버 권위 writer가 trajectory·SimulationFacing·phase를 commit한다.
2. 사거리 안에서 타깃을 획득하면 해당 행동 회차의 이동/공격 계약에 따라 진행을 멈추고 타격을 예약한다.
3. HP는 권위 due·대상·회차 경계에서 확정한다. 로컬 Animation Event나 tracer 도착은 HP를 다시 쓰지 않는다.
4. 확정 결과와 표현은 회차/타격 identity로 연결하며 취소·사망·경기 종료의 수명 경계를 구분한다.
5. 전투 후에는 원래 이동 목표·권위 위치에서 복귀를 판단하고 checkpoint·타일 도착·점령을 중복 소비하지 않는다.

유닛 간 점유는 이동 차단 근거가 아니다. 점령은 타일 도착 처리에 귀속되며, 스폰 자격은 이동과 별도다. [유닛 동작 원본](GameSystemRules/GameSystemRules_Units.md)·[동기화 원본](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), [이전 Lerp·선점·즉시 피해 흐름](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-combat-flow)을 구분한다.

<a id="deep-authoritative-locomotion-구현-구조-2026-08-04-editor-조건부-pass"></a>

#### Deep Authoritative Locomotion 구현 구조

서버 writer는 10°/15° fallback 정렬, 이동 금지 틱의 진행량 보존, target-acquire 우선, endpoint `NoIntent`, 경기 단위 mode 고정과 Client Simulation Root write 금지의 계약을 유지한다. 연속 코너의 trajectory 계산은 서버가 담당하고 NetworkTransform과 Visual Root는 commit 결과를 복제·표현한다. 아래 구조 설명을 전체 실기 수용으로 해석하지 않는다. [이전 조건부 검증 경위](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-locomotion-receipt).

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

`UnitTrajectoryStep`은 candidate position·facing·진행량·hold/waypoint 상태를 한 결과로 전달하고, `UnitPathCheckpointTracker`는 곡선이 waypoint 중심을 정확히 밟지 않아도 실제 trajectory의 통과를 순서대로 한 번만 소비한다. Planner는 최초 Simulation Root point와 A* logical path를 입력으로 logical path corridor를 만들며 경유 타일 중심 통과를 강제하지 않는다. 1차 안전 판정은 Simulation Root point sweep이고, 실패하면 안전한 후보 또는 재경로로 닫는다. `MoveSpeed`는 실제 trajectory 거리/초로 적분한다. 위치 진행 방향과 `SimulationFacing`은 같은 접선에서 계산해 오차를 1° 이내로 제한한다. 공통 최대 회전 속도는 270°/s이며, target acquire는 해당 틱의 candidate position으로 판정한다. 획득이 확정된 틱에는 candidate pose까지만 commit하고 `NoIntent`를 게시하며 다음 틱의 추가 이동을 금지한다. 150° 이상의 반전은 정지 정렬 fallback이다.

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

서버 pose seam·Simulation/Visual Root seam과 순수 이동 reducer를 바탕으로 경기 고정 `ReducerAuthoritative` single-writer·이동 phase 복제를 연결한다. `UnitServerTrajectoryPlanner`와 공통 commit adapter가 A*·Chase·PendingRepath·PostCombatResume, 거리 기반 waypoint 소비·candidate target acquire·SimulationFacing 반영을 담당한다. `UnitNavigationObjective`는 이동 코루틴보다 긴 목표 수명을 관리하며 동일 경로 재평가·막힘 대기·진전 또는 walkability revision의 이력 초기화를 분리한다. 현재 위치와 맞지 않는 PendingPath는 다시 요청하고, Held/Frozen·level snapshot은 서버 소유 값으로 전달한다. endpoint가 이미 NoIntent이면 동일 snapshot을 중복 게시하지 않는다.

SOLO 일반 공격의 positive sorted offsets는 연속 occurrence clock에서 captured target/sequence·절대 due와 결합한다. ZERO/invalid/unsorted는 기존 Legacy fallback이며 Torrent/Healer는 이 일반 clock 대상이 아니다. SOLO Legacy pursuit는 remaining distance clamp와 actual reached 판정으로 checkpoint를 정확히 한 번 소비한다. 공통 복귀의 endpoint 중복 방지·pursuit 흐름은 이 기존 구조를 유지한다. Bloom은 별도 HealNow에서 accepted StartedAt+MarkerOffset absolute due를 기다리고 yield 뒤 caster/authority/active 회차·finite time을 재확인해 CastHeal을 단일 실행한다.

이는 현재 기술 구조 요약이며 과거 compile/native 인계나 전체 수용을 선언하지 않는다. [이전 목표·상태 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-action-01), [기존 복원·SOLO 근거](_Tasks/2026-10-14/10_04_post-merge-unit-regression-audit/Research.md), [현행 Units 계약](GameSystemRules/GameSystemRules_Units.md)과 [현재 상태](PROJECT_STATUS.md)를 구분한다.

<a id="legacy-피격-표현-큐-hit-presentation-queue-2026-07-12-전투-타격-타이밍-동기화"></a>

##### 공격 결과와 Legacy 피격 표현 경로

권위 HP 확정과 화면의 피해 텍스트·피격 VFX·타격 반응은 별도 책임이다. 정규 공격 표현은 서버 일정/확정 결과 identity와 표현 시계를 사용한다. `Unresolved/LegacyFallback` 경로는 정규 이행 완료와 구분하며 기존 source 표현을 타입 미이행만으로 차단하지 않는다.

`HitPresentationQueue`의 공격자별 FIFO는 이전 경로의 기술 근거다. 이를 현재 모든 유닛의 Animation Event 방출 규칙으로 일반화하지 않는다. `EffectManager`의 hit/building/tracer 프리셋과 `FloatingHpTextSpawner`는 표현 진입점이며 실제 연결은 [Matrix](Assets/UnitCombatAssetMatrix.md)·[VFX 목록](Assets/VFXSFXList.md), 현재 전달 계약은 [전투 동기화 규칙](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)을 따른다. [이전 FIFO·에셋 교체 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-legacy-hit).

<a id="유닛-애니메이션-상태-동기화-2026-07-13-엣지-rpc--networkvariable-레벨-동기화"></a>

#### 유닛 애니메이션 상태 동기화

`NetworkUnit`의 서버 쓰기/전체 읽기 상태 값과 `OnValueChanged`·spawn 현재값 적용으로 늦은 구독을 복구한다. View의 Animator 초기화보다 입력이 먼저 온 경우 `UnitView.Initialize` 이후 `ReapplyAnimStateToView()`로 멱등 재적용한다. edge-only RPC의 수신 여부를 현재 상태의 유일 근거로 삼지 않는다.

일반 공격은 독립 Attack 레벨 값만으로 클립을 시작하지 않고 정규 공격의 타깃·revision·표현 허가를 따른다. Bloom은 별도 atomic heal snapshot의 회차·시각·priority와 late-init 복구를 사용한다. 서버 피해·Root 회전은 애니메이션 상태와 분리하며 `_combatAnimationSent`의 피해/타깃 게이팅과 표현 시작을 혼동하지 않는다. [유닛 규칙 22](GameSystemRules/GameSystemRules_Units.md#애니메이션-상태-동기화-규칙), [이전 스폰 레이스·적용 경위](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-anim-state).

<a id="특수-공격-전략-핸들러-구조-2026-07-17-도끼병-휩쓸기형-aoe"></a>

#### 특수 공격 전략 핸들러 구조

`ISpecialAttackBehavior.Apply(SpecialAttackContext)`와 `SpecialAttackRegistry`가 UnitType별 특수 처리를 선택한다. 일반 단일 피해와 특수 대상도 공통 피해·이벤트·사망 처리 경계로 수렴해 서버 HP writer를 중복하지 않는다. 범위·피해 의미는 [유닛 규칙의 특수 공격](GameSystemRules/GameSystemRules_Units.md), 실제 구현·연결은 [Matrix](Assets/UnitCombatAssetMatrix.md)를 따른다.

핸들러에는 `IEntityPositionProvider`와 순수 튜닝 값을 주입한다. Infrastructure의 `SpecialAttackConfig`를 Bootstrap에서 읽어 Application에 값으로 전달하며 역방향 SO 참조를 만들지 않는다. 에셋은 Inspector에서 배선하고 필요할 때 기본 CreateAssetMenu로 생성한다. 에셋 생성과 씬 주입은 별도이며 미배선 fallback과 실제 선택값을 구분한다.

Sweep·Wave·Blast/DoT·Inferno·Quake와 별도 Healer 경로를 초기 BattleAxe 단독 등록 예시로 대체하지 않는다. AoE 원자 표현은 확정 묶음/manifest 계약을 따르며 과거 FIFO의 배열 길이 추정만으로 전체 완료를 주장하지 않는다. [초기 핸들러/큐·도구 경위](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-special).

<a id="건물-배치-시스템-mvp-phase-1"></a>

### 건물 배치 시스템

입력·패널·UseCase·Factory를 분리해 배치와 데이터/표현 생성을 연결한다. 배치 자격·경제 조건·업그레이드·생산은 [건물 규칙](GameSystemRules/GameSystemRules_Buildings.md)과 [건물 패널](GameSystemRules/GameSystemRules_BuildingPanelsUI.md)을 따른다. 과거 MVP1의 자원·생산 미포함 표기는 [당시 범위](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-building-stage)이며 현재 지원 범위를 뜻하지 않는다.

<a id="건물-타입-2026-05-17-26종-확장"></a>

#### 건물 타입과 메타데이터

`BuildingType`과 종족 키로 실제 건물 자산·기능을 선택한다. 종족별 AutoTower와 공격하지 않는 HealShrine은 별도이며 Research는 유닛 강화 건물이다. 현재 자산명·enum 대응은 [AssetList](Assets/AssetList.md), 수치는 [StatsReference](StatsReference.md)를 따른다.

Domain의 `BuildingTypeHelper`는 생산 여부·단계·다음 단계 메타데이터를 공통 lookup으로 조회한다. `IsProductionBuilding`·`GetStage`·`GetNextStage`·`CanUpgrade`·`CanShowActionPanel`의 조건을 별도 분기 사본으로 관리하지 않는다. [이전 타입 확장·미구현 주석 예시](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-building-types).

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
    -   `BuildingFactory`가 생성 오브젝트를 등록하고 데이터/표현을 연결한다. 삭제된 BuildingView 컴포넌트 초기화를 현재 배선 단계로 요구하지 않는다. [이전 설명](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-buildingview).

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

건물의 공통 배치 처리에서 인접 타일을 건물 팀 소유로 변경한다. 이미 같은 팀인 타일은 건너뛴다.
```csharp
// BuildingPlacementUseCase.ClaimAdjacentTiles() 내부
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

`UnitProductionStats`는 유닛 생산 시간·비용·인구를 조회하며 실제 값은 설정과 [StatsReference](StatsReference.md)를 따른다. 단일 Pistoleer switch 예시를 전 roster의 fallback으로 사용하지 않는다.

`ProductionState`는 건물별 통합 `PendingQueue`와 자동 순환 `AutoTypes/AutoIndex`, 랠리 목표를 보유한다. `QueueSlot`은 타입·골드 차감 여부·진행도를 구분하며 `CurrentIsAuto`는 현재 항목과 자동 목록에서 파생한다. 큐 상한·골드 차감·모드 전환은 [생산 패널 규칙](GameSystemRules/GameSystemRules_BuildingPanelsUI.md)이 원본이다. [이전 코드 예시](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-production-domain).

#### UseCase 구조

| UseCase | 역할 |
|---------|------|
| `ResourceUseCase` | 팀별 골드 관리 (초기화, 차감/추가/조회) |
| `PopulationUseCase` | 인구수 계산 (최대=보유 타일, 사용=건물+유닛) |
| `UnitProductionUseCase` | 생산 큐/타이머/자동-수동 모드/랠리포인트 |

<a id="생산-흐름-상세"></a>

#### 생산 흐름

건물 등록 → 입력을 UseCase에 전달 → 통합 큐/자동 목록 조정 → 골드·인구 검증과 차감 → Tick 진행 → 인접 생성 타일 확인 → 생산 완료 이벤트 → UnitFactory 생성·초기화 순서로 연결한다.

수동 등록 시 자동 모드는 해제하지만 이미 골드를 차감한 항목은 수동 큐로 이관해 생산을 계속한다. 자동 모드에서 자원 부족 시 자동 등록/생산 취소 처리는 [생산 패널 규칙](GameSystemRules/GameSystemRules_BuildingPanelsUI.md)을 따른다. 생산 완료 후 스폰 불가 대기는 진행 전 골드·인구 부족 처리와 구분한다. [옛 ManualQueue·즉시 취소/대기 흐름](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-production-flow).

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
    // OnUnitProduced 구독 → 랠리 전용 목적지 질의 후 자동 이동 처리
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
  ├─ 예 → 랠리 전용 목적지 선택 → 선택 칸으로 이동
  │        ↓ OnMoveComplete (이미 도착/대체 후보 없음이면 랠리 생략)
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
- **배럭 선택(팝업 열림)**: 모든 자동 숨김 타이머를 중지하고 다른 배럭 마커를 숨긴 뒤 해당 배럭 마커만 표시 (ProductionPanelUI → ShowRallyMarker)
- **팝업 닫힘/지정 후 다른 맵 위치 클릭**: 모든 마커 즉시 숨김. 빈 곳·막힌 타일 탭도 포함 (ProductionPanelUI → HideAllRallyMarkers)
- **배럭 타일에 랠리포인트 설정**: 랠리포인트 해제 + 마커 Destroy
- **배럭 파괴**: 마커 Destroy
- **마커 프리팹**: GameConfig.RallyPointPrefab (Inspector에서 할당)
- **마커 위치/회전**: GameConfig.RallyMarkerOffset / RallyMarkerEuler (Inspector 조정)
- 자동 숨김 코루틴은 배럭 Id별 사전으로 관리한다. 싱글·멀티 모두 로컬 팀 이벤트만 처리해 상대/AI 마커가 표시·타이머를 간섭하지 않도록 한다.

<a id="랠리포인트-bfs-빈-타일-탐색"></a>
#### 랠리 목적지 선택과 재경로
랠리 이동은 공성용 `FindPathToNearestEmptyTile`과 구분되는 `UnitMovementUseCase.RequestRallyPath` 질의를 사용한다. 생산 시 `ProductionTicker`와 이동 중 `UnitView`의 권위 재경로가 같은 질의를 호출한다.
```
Ring 0: 랠리포인트 자체 (1타일)
Ring 1: 인접 6타일
Ring 2: 그 바깥 12타일
Ring 3: 그 바깥 18타일 (최대 3칸)
```
- 이동 가능하고 도달 가능한 칸만 후보로 삼는다. 첫 성공 고리 안에서 경로 길이가 가장 짧은 칸을 선택하며, 동률은 고정 방향 순서를 유지한다.
- 원래 랠리 칸과 실제 이동 종점을 분리한다. `UnitView.MoveToRally`가 원래 랠리를 보유하고, 이동 도중 건설로 경로가 무효화되면 최신 권위 Root·환경 revision 검증을 유지하면서 목적지를 다시 선택한다.
- `PathFound`·`CurrentTileSelected`·`NoReachableCandidate`·`ProviderFailure` 결과를 구분한다. 현재 칸 도착과 후보 없음은 랠리 종료 후 기존 완료 콜백을 통해 적 성 진군으로 이어지며, 공급 실패·권위 입력 오류를 정상 도착으로 오인하지 않는다.
- 목적지 변경 시 objective·실제 종점·대기 경로를 수용 경계에서 함께 바꾸고 기존 공간 commit·무진전 guard를 유지한다. 일반 성 이동·전투 접근의 이동 불가 종점 덧붙임 계약은 별도다.
- 공성 방향 폴백은 기존 `ProductionTicker.FindPathToNearestEmptyTile`을 계속 사용한다. 랠리의 후보 소진 → 적 성 진군 정책을 이 공용 폴백의 동작과 혼동하지 않는다.
- 지정 가능 좌표는 `UnitProductionUseCase.CanSetRallyPointAt`으로 판정한다. 입력·서버 RPC·상태 저장 경로가 격자 밖과 `TileKind.Blocked`를 거부하며, 입력은 토스트를 띄우고 지정 모드를 유지한다. 건물·금광 칸은 지정할 수 있다. 제품 동작의 원본은 [건물 규칙](GameSystemRules/GameSystemRules_Buildings.md) 「랠리포인트 시스템」이다.

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

현재 초기 자원은 GameConfig.StartingGold를 ResourceUseCase에 전달한다. 맵 InitialGold의 생성·전송과 실제 시작 자원 연결은 별개이며 후자는 ROADMAP의 경제 후속 범위다. 별도의 맵 테스트 모드 분기를 두지 않는 계약을 유지하고 경제 설정의 기본값·fallback과 경기 맵 적용을 구분한다. [현재 수치](StatsReference.md#자원-시스템)·[맵 전달 계약](TechnicalDesignDocument_Network.md), [필드 제거 당시 설계 선행 기록](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-tdd-economy-history).

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
Unity Version: 6000.3.5f2
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

에디터에서 다음 경기의 실패 경로를 예약해 재경기·최초 경기·싱글 실패 화면을 확인하는 도구다. 화면 동작은 [MatchFlowUI](GameSystemRules/GameSystemRules_MatchFlowUI.md), 실패 사유는 [RandomMap](GameSystemRules/GameSystemRules_RandomMap.md)을 따른다.

현재 메뉴는 `Hexiege > Debug > Failure` 아래에 있다.

| 항목 | 사용 범위 |
|---|---|
| `다음 재경기/① 연결 끊김 (Disconnected)` | 다음 멀티 재경기 전송 실패 |
| `다음 재경기/② 해시 불일치 (HashMismatch)` | 다음 멀티 재경기 전송 실패 |
| `다음 재경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)` | 다음 멀티 재경기 대기/재전송 후 timeout |
| `다음 재경기/④ 수락 전송 예외 (AcceptSendException)` | 재경기 수락 전송의 기존 catch 경로 |
| `다음 최초 경기/① 연결 끊김 (Disconnected)` | 다음 멀티 최초 경기 전송 실패 |
| `다음 최초 경기/② 해시 불일치 (HashMismatch)` | 다음 멀티 최초 경기 전송 실패 |
| `다음 최초 경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)` | 다음 멀티 최초 경기 timeout |
| `싱글 맵 준비 (다음 1회)` | 다음 싱글 최초/다시하기의 맵 준비 실패 |
| `싱글 맵 투영 (다음 1회)` | 다음 싱글 최초/다시하기의 맵 투영 실패 |
| `현재 설정 확인` | 예약 저장소 전체 확인 |
| `해제` | 예약을 해제해 정상 동작으로 복귀 |

1. 실행할 경기와 실패 단계를 선택하고 해당 메뉴를 한 번 누른다. 모달 알림이 아니라 Console의 예약 로그를 확인한다.
2. 최초 경기 또는 결과 화면의 다시하기를 진행한다. 예약을 소비하면 자동 해제되는 일회성 설정이다.
3. 비교할 다른 화면을 보려면 다시 예약한다. 정상 동작으로 돌릴 때는 해제를 사용한다.

파일 전체가 Editor 가드 안이고 EditorPrefs를 사용해 씬·프리팹에 값을 남기지 않는다. 릴리스·실기기 빌드에는 이 도구가 없으며 에디터 한쪽에 건 예약으로 양 기기의 모든 실패를 검증했다고 주장하지 않는다. 진짜 실패 경로를 사용하지만 로그의 `[강제 실패] …(실제 장애가 아니다)`/Forced 필드로 실제 장애와 구별한다.

<a id="사용-전에-unity-의-error-pause-를-끈다-2026-09-27-추가--위-절차성질-목록은-한-글자도-지우지-않는다"></a>

#### Error Pause와 실패 로그

Disconnected·HashMismatch·ResponseTimeout 및 싱글 준비/투영은 실제 실패 경로의 ERROR 로그로 Error Pause가 걸릴 수 있으므로 사용 전에 Console의 Error Pause를 끈다. 에디터 정지를 코드의 카운트다운/복귀 실패로 오독하지 않는다. ④만 사용할 때는 기존 catch의 WARN 경로이므로 이 준비 단계의 예외다.

<a id="넷째-사유-추가--④-수락-전송-예외-2026-09-28-4차-추가--위-절차성질메뉴-경로-목록은-한-글자도-지우지-않는다"></a>

#### 수락 전송 예외 예약

④는 맵 전송 사유와 다른 별도 예약이다. 에디터에서 원래 수락 전송 catch를 재현하며 다른 강제 전송 사유를 대신하지 않는다. 현재 설정 확인/해제는 이 저장소도 함께 다룬다. 개별 실패 화면·복귀 사양은 UI 규칙을 따른다.

<a id="다섯째여섯째-사유--싱글플레이-전용-2종-2026-09-29-2차-추가--위-절차성질메뉴-경로-목록과-④-소절은-한-글자도-지우지-않는다"></a>

#### 싱글 준비·투영 예약

싱글 준비/투영은 멀티에 영향을 주지 않고 먼저 오는 싱글 최초/다시하기 경로가 소비한다. 준비와 투영을 동시에 예약하면 준비 실패로 투영 단계에 도달하지 않아 투영 예약은 다음 경기까지 남는다. 투영 화면만 보려면 해제 후 투영만 예약한다. 두 화면을 보려면 각각 새로 예약하며 싱글에는 자동 복귀 타이머를 만들지 않는다.

당시 발화 시각·관측 횟수·실기/에디터 확인 한계와 도입 과정은 [원문 이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-failure-01)에 보존한다. 도구 사용법 정리는 새 native·기기·컴파일 경고0 또는 자연 실패 빈도 검증이 아니다.

<a id="변경-이력"></a>

## 📝 변경 이력

버전별 변경 원문은 [보존 이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-versions-01)에 둔다. 현행 기술은 각 본문, 현재 상태는 [Status](PROJECT_STATUS.md), 주요 완료 시점은 [작업 이력](WORK_HISTORY.md)을 따른다.
