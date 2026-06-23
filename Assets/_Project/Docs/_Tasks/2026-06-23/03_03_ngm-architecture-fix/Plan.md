# Plan: NetworkGameManager 아키텍처 위반 수정

## 작업 목적

`NetworkGameManager`(Infrastructure)가 `SceneLoader`(Presentation)를 직접 참조하는 아키텍처 위반을
이미 프로젝트에 정착된 GameEvents 이벤트 패턴으로 교체한다.

---

## 근거 규칙

- **Clean Architecture**: 하위 레이어(Infrastructure)는 상위 레이어(Presentation)를 참조 불가
- **UI 규칙 L-4 주석**: "Infrastructure에서 발생하는 씬 전환은 GameEvents(Application)를 경유해 Presentation이 ShowLoading을 호출하도록 한다"

---

## 수정 항목

### 1. `GameEvents.cs` — `OnNetworkBackToLobby` Subject 추가

**위치**: 멀티플레이 재경기 이벤트 섹션 하단 (OnNetworkRematchStarting 아래)

```csharp
/// <summary>
/// 네트워크 게임 종료 후 로비로 복귀할 때 발행하는 이벤트.
/// 발행: NetworkGameManager.BackToLobby (NGO Shutdown 직전)
/// 구독: GameEndUI (SceneLoader.Load 호출)
///
/// 왜 이벤트로 보내는가:
///   BackToLobby는 Infrastructure 레이어지만, 씬 전환(SceneLoader)은 Presentation 소속이다.
///   레이어 방향을 보호하기 위해 GameEvents(Application)를 경유한다(UI 규칙 L-4 주석).
/// </summary>
public static readonly Subject<string> OnNetworkBackToLobby = new Subject<string>();
```

---

### 2. `NetworkGameManager.cs` — SceneLoader 의존 제거

**제거**: `using Hexiege.Presentation;` (Line 28)

**교체**: `BackToLobby()` 메서드의 `SceneLoader.Load(lobbySceneName)` → 이벤트 발행

```csharp
// 변경 전
SceneLoader.Load(lobbySceneName);

// 변경 후
GameEvents.OnNetworkBackToLobby.OnNext(lobbySceneName);
```

**추가**: `using Hexiege.Application;` (GameEvents 사용을 위해)
- 이미 있는지 확인 후 없으면 추가

---

### 3. `GameEndUI.cs` — `OnNetworkBackToLobby` 구독 추가

**위치**: `Start()` 또는 `OnEnable()` 구독 등록부, 기존 재경기 이벤트 구독 아래

```csharp
GameEvents.OnNetworkBackToLobby
    .Subscribe(sceneName => SceneLoader.Load(sceneName))
    .AddTo(this);
```

---

## 위험 요소

- `GameEndUI`가 Game 씬에서 항상 존재한다는 전제가 있어야 이벤트가 수신된다.
  현재 `BackToLobby`가 호출되는 시점(게임 종료 후)에는 `GameEndUI`가 활성 상태이므로 문제없다.
- `NetworkGameManager`에 `using Hexiege.Application`이 이미 있는지 확인 필요.
