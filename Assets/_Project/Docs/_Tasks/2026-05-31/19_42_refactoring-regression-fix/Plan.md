# Plan: 리팩토링 회귀 항목 수정

## 이 문서가 다루는 것

이 문서는 Research.md에서 발견된 회귀 항목 1건(FindFirstObjectByType 캐시화 패턴 위반 3곳)을 어떻게 수정할 것인지 설명하는 작업 지시서입니다.

쉽게 설명하면 이렇습니다.
- 2026-05-24 리팩토링에서 "씬을 매번 탐색하지 말고 게임 시작 시 한 번만 찾아서 기억해두자"는 패턴을 확립했습니다.
- 그 이후 추가된 코드 3곳에서 이 패턴을 따르지 않고 다시 매번 탐색하는 방식을 사용하고 있습니다.
- 이 문서는 그 3곳을 동일한 패턴으로 맞추는 방법을 설명합니다.

수정 범위가 작고(3개 파일, 각 10줄 미만 변경), 게임 동작에는 영향이 없는 내부 구조 개선입니다.

> **근거**: CLAUDE.md / 프로젝트 MEMORY.md "Architecture Rules" — OnNetworkSpawn 단일 캐시 패턴. 2026-05-24 리팩토링 그룹 4에서 확립된 프로젝트 표준.

> **GameSystemRules.md 관련**: 이번 수정은 게임 시스템 규칙(생산, 이동, 전투 등)을 변경하지 않습니다. 순수하게 아키텍처 일관성과 모바일 성능을 위한 코드 내부 구조 개선입니다.

---

## ⚠️ 기존 로직 제거 항목 (최상단 명시)

본 수정에서 제거되는 코드는 다음과 같습니다:

| 파일 | 제거되는 라인 | 이유 |
|------|-------------|------|
| `NetworkUnit.cs` (라인 178) | `var bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();` | 새로 추가되는 `_bootstrapper` 캐시 필드로 대체 |
| `NetworkGameEndController.cs` (라인 160) | `var ngm = FindFirstObjectByType<NetworkGameManager>();` | 새로 추가되는 `_networkGameManager` 캐시 필드로 대체 |
| `ReconnectionHandler.cs` (라인 188) | `NetworkGameEndController endController = FindFirstObjectByType<NetworkGameEndController>();` | 새로 추가되는 `_networkGameEndController` 캐시 필드로 대체 |

**제거해도 안전한 근거**: 3곳 모두 OnNetworkSpawn에서 캐시한 값으로 완전히 대체되므로, 런타임에 null이 될 경우를 제외하면 동작이 동일합니다. 기존 null 체크 로직은 그대로 유지합니다.

---

## 수정 항목 1: NetworkUnit.cs — _bootstrapper 캐시 추가

### 현재 상태

`RegisterToFactory()` 메서드 내부에서 GameBootstrapper를 매번 씬에서 탐색합니다. 이 메서드는 유닛이 생성될 때마다 호출되므로, 10마리 유닛이 있으면 10번 씬 탐색이 발생합니다.

```
현재 흐름:
OnNetworkSpawn() → RegisterToFactory(unitId)
                         ↓
                 FindFirstObjectByType<GameBootstrapper>()  ← 씬 전체 탐색
```

### 수정 후 상태

```
수정 후 흐름:
OnNetworkSpawn() → _bootstrapper 캐시 (1회)
                → RegisterToFactory(unitId)
                         ↓
                 _bootstrapper (캐시된 값 사용)  ← 즉시 반환
```

### 변경 내용

**1단계: 필드 추가 (클래스 상단 필드 선언부)**

```csharp
// GameBootstrapper 참조를 OnNetworkSpawn에서 1회만 탐색하여 캐시한다.
// 이후 RegisterToFactory 등에서 재탐색 없이 재사용.
private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;
```

**2단계: OnNetworkSpawn에서 캐시**

`if (IsServer) return;` 직전(또는 직후, 클라이언트 전용 경로)에 아래 코드를 추가합니다.

```csharp
// OnNetworkSpawn에서 1회만 씬을 탐색하여 캐시.
// RegisterToFactory는 클라이언트 측에서만 호출되므로
// 서버 조기 리턴 이전 또는 클라이언트 경로 시작 시점에 캐시한다.
_bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
```

> **캐시 위치 결정**: `if (IsServer) return;` 이전에 캐시하면 서버도 불필요하게 탐색하므로,
> 해당 라인 이후(클라이언트 전용 경로 시작부)에 추가하는 것이 더 효율적입니다.

**3단계: RegisterToFactory에서 캐시 사용**

```csharp
// 변경 전
var bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

// 변경 후
var bootstrapper = _bootstrapper;
```

null 체크 코드(`if (bootstrapper == null) { ... }`)는 그대로 유지합니다.

---

## 수정 항목 2: NetworkGameEndController.cs — _networkGameManager 캐시 추가

### 현재 상태

`OnGameEndServer()` 이벤트 핸들러 내부에서 NetworkGameManager를 씬에서 탐색합니다. 게임 종료는 1회성이므로 성능 영향은 미미하나, 리팩토링에서 확립한 패턴과 일관성이 깨집니다.

### 수정 후 상태

OnNetworkSpawn의 서버 전용 경로에서 NetworkGameManager를 1회 캐시하고, 이후 사용합니다.

### 변경 내용

**1단계: 필드 추가 (클래스 상단 필드 선언부)**

```csharp
// NetworkGameManager 참조를 OnNetworkSpawn에서 1회만 탐색하여 캐시한다.
// 서버 전용 필드 — 클라이언트에서는 null 상태를 유지해도 무방.
private Hexiege.Infrastructure.NetworkGameManager _networkGameManager;
```

**2단계: OnNetworkSpawn의 서버 경로에서 캐시**

`if (IsServer)` 블록 내, 이벤트 구독(`GameEvents.OnGameEnd.Subscribe(...)`) 코드 직전에 추가:

```csharp
// 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시.
// OnGameEndServer에서 매번 탐색하지 않도록 미리 보관.
_networkGameManager = FindFirstObjectByType<Hexiege.Infrastructure.NetworkGameManager>();
```

**3단계: OnGameEndServer에서 캐시 사용**

```csharp
// 변경 전
var ngm = FindFirstObjectByType<NetworkGameManager>();

// 변경 후
var ngm = _networkGameManager;
```

null 체크 코드(`if (ngm != null) ...`)는 그대로 유지합니다.

---

## 수정 항목 3: ReconnectionHandler.cs — _networkGameEndController 캐시 추가

### 현재 상태

`WaitAndForceWin()` 코루틴 내부에서 NetworkGameEndController를 씬에서 탐색합니다. 파일 헤더 주석에도 "FindFirstObjectByType으로 탐색"이라고 의도적으로 명시되어 있으나, 이는 작성 당시 캐시 패턴이 적용되기 전 방식을 따른 것입니다.

### 수정 후 상태

OnNetworkSpawn의 서버 경로에서 NetworkGameEndController를 1회 캐시하고, 이후 사용합니다.

### 변경 내용

**1단계: 필드 추가 (클래스 상단 필드 선언부)**

```csharp
// NetworkGameEndController 참조를 OnNetworkSpawn에서 1회만 탐색하여 캐시한다.
// WaitAndForceWin 코루틴에서 재탐색 없이 사용.
private NetworkGameEndController _networkGameEndController;
```

**2단계: OnNetworkSpawn의 서버 경로에서 캐시**

`!IsServer` 체크 이후 서버 초기화 블록(NetworkManager 콜백 등록 직전)에 추가:

```csharp
// 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시.
// WaitAndForceWin 코루틴에서 매번 탐색하지 않도록 미리 보관.
_networkGameEndController = FindFirstObjectByType<NetworkGameEndController>();
```

**3단계: WaitAndForceWin에서 캐시 사용**

```csharp
// 변경 전
NetworkGameEndController endController = FindFirstObjectByType<NetworkGameEndController>();

// 변경 후
NetworkGameEndController endController = _networkGameEndController;
```

null 체크 코드(`if (endController == null) { Debug.LogError(...) }`)는 그대로 유지합니다.

**4단계: 파일 헤더 주석 수정**

```
// 변경 전
//   - NetworkGameEndController는 FindFirstObjectByType으로 탐색

// 변경 후
//   - NetworkGameEndController는 OnNetworkSpawn에서 1회 캐시하여 재사용
```

---

## 전체 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Infrastructure/Network/NetworkUnit.cs` | `_bootstrapper` 필드 추가, OnNetworkSpawn 클라이언트 경로에서 캐시, RegisterToFactory에서 캐시 사용 | 수정 |
| `Infrastructure/Network/NetworkGameEndController.cs` | `_networkGameManager` 필드 추가, OnNetworkSpawn 서버 경로에서 캐시, OnGameEndServer에서 캐시 사용 | 수정 |
| `Infrastructure/Network/ReconnectionHandler.cs` | `_networkGameEndController` 필드 추가, OnNetworkSpawn 서버 경로에서 캐시, WaitAndForceWin에서 캐시 사용, 헤더 주석 수정 | 수정 |

---

## 위험 요소 평가

| 위험 | 평가 | 대응 |
|------|------|------|
| OnNetworkSpawn 시점에 대상 오브젝트가 씬에 없는 경우 | 낮음 — 기존에도 동일한 상황에서 null 반환. 기존 null 체크 로직이 동일하게 동작 | null 체크 코드 유지 |
| 캐시 후 대상 오브젝트가 Destroy되어 dangling reference 발생 | 낮음 — 세 대상 모두 게임 씬 생명주기 동안 유지되는 싱글턴 성격의 오브젝트 | 별도 대응 불필요 |
| 변경 후 멀티플레이 동기화 영향 | 없음 — 씬 탐색 방법 변경일 뿐, 호출 결과는 동일 | 기본 플레이 검증으로 확인 |

---

## 검증 체크리스트

- [ ] Unity Editor 컴파일 에러 0건
- [ ] 멀티플레이: 유닛 생산 후 클라이언트 화면에 유닛 정상 표시
- [ ] 멀티플레이: 게임 종료(포기) 시 양쪽 게임 종료 화면 정상 표시
- [ ] 멀티플레이: 재경기 흐름(요청/수락/거절) 정상 동작
- [ ] 멀티플레이: 재연결 흐름(오랜 시간 비연결 후 강제 승리) — 가능하면 확인
- [ ] 콘솔에 NullReferenceException 0건
