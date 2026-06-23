# Research: NetworkGameManager 아키텍처 위반 수정

## 작업 목적

이 작업은 Clean Architecture 규칙을 위반하는 코드를 수정하기 위한 것입니다.

현재 `NetworkGameManager`(Infrastructure 레이어)가 `SceneLoader`(Presentation 레이어)를 직접 참조하고 있습니다.
Clean Architecture에서 하위 레이어(Infrastructure)는 상위 레이어(Presentation)를 참조할 수 없습니다.
이 위반은 이전 세션에서 LoadingIndicator 전면 적용 작업 중 발생했습니다.

---

## 위반 현황

### 파일: `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs`

**Line 28:**
```csharp
using Hexiege.Presentation; // SceneLoader (씬 전환 + 로딩 인디케이터 자동 표시)
```

**Line 625-627 (BackToLobby 메서드):**
```csharp
// 5. 씬 전환 — SceneLoader.Load 가 로딩 인디케이터를 자동 표시한다.
//    (이 SceneManager.LoadScene 은 NGO 씬 매니저가 아닌 일반 씬 전환이므로 SceneLoader 대상)
SceneLoader.Load(lobbySceneName);
```

### BackToLobby 호출 맥락

`BackToLobby`는 `NetworkGameEndController`(Infrastructure)에서 호출된다.
게임 종료 후 NGO 연결 해제 → Lobby 씬 전환을 담당하는 흐름이다.

---

## 기존 패턴 분석

이미 프로젝트에서 동일한 문제를 해결한 선례가 있다:

### 재경기(Rematch) 로딩 패턴

`NetworkGameEndController`가 재경기 씬 전환 직전에 로딩 인디케이터를 띄워야 하는 상황에서
Infrastructure → Presentation 직접 참조 대신 아래 패턴을 사용했다:

1. `GameEvents.OnNetworkRematchStarting` Subject 발행 (Infrastructure → Application)
2. `GameEndUI`(Presentation)가 구독하여 `ShowLoading(true, "재경기 준비 중...")` 호출

이 패턴이 `GameSystemRules_UI.md` 규칙 L-4 주석에도 명시되어 있다:
> Infrastructure(NetworkBehaviour)에서 발생하는 씬 전환(재경기 등)은 UIManager(Presentation)를
> 직접 참조하지 않고 GameEvents(Application)를 경유해 Presentation(GameEndUI)이 ShowLoading을 호출하도록 한다.

---

## 수정 방향

**BackToLobby 씬 전환도 재경기 패턴과 동일한 방식으로 처리한다.**

1. `GameEvents`에 `OnNetworkBackToLobby` Subject 추가 (payload: `string sceneName`)
2. `NetworkGameManager.BackToLobby()`에서 이벤트 발행
3. Presentation 레이어의 적절한 구독자가 `SceneLoader.Load()` 호출

### 구독자 선정

`BackToLobby`는 게임 종료 후 로비로 이동하는 상황이므로 `GameEndUI`가 구독하기에 적합하다.
(재경기 관련 이벤트들도 모두 `GameEndUI`가 처리하는 패턴과 일치)

---

## 영향 범위

| 파일 | 변경 내용 |
|------|-----------|
| `GameEvents.cs` | `OnNetworkBackToLobby` Subject 추가 |
| `NetworkGameManager.cs` | `using Hexiege.Presentation` 제거, `SceneLoader.Load` → 이벤트 발행으로 교체 |
| `GameEndUI.cs` | `OnNetworkBackToLobby` 구독 추가 |
