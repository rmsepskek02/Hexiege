# Testcase: Game UI Lifecycle Framework

## 테스트 대상
- IGameUI 인터페이스 + GameUIManager 기반 UI 생명주기 관리
- 게임 종료 시 UI 숨김 처리
- 재시작 시 UI 초기화 처리

---

## TC 목록

### SINGLE-1: 게임 종료 시 생산 패널 자동 닫힘

**전제:** 게임이 진행 중이고, 배럭을 클릭하여 생산 패널이 열려 있는 상태

**동작:**
1. 상대방 캐슬이 파괴되거나, 내 캐슬이 파괴되어 게임이 종료됨

**기댓값:**
- 생산 패널이 즉시 닫힘
- 게임 종료 팝업(승리/패배)이 표시됨

**결과:** PASS

---

### SINGLE-2: 게임 종료 시 건물 배치 팝업 자동 닫힘

**전제:** 게임이 진행 중이고, 빈 타일을 탭하여 건물 배치 팝업이 열려 있는 상태

**동작:**
1. 게임이 종료됨 (어느 팀이든 캐슬 파괴)

**기댓값:**
- 건물 배치 팝업이 즉시 닫힘
- 게임 종료 팝업이 표시됨

**결과:** PASS

---

### SINGLE-3: 게임 종료 시 HUD 유지

**전제:** 게임이 진행 중인 상태

**동작:**
1. 게임이 종료됨

**기댓값:**
- 골드/인구수/타일 카운트 HUD는 사라지지 않고 그대로 유지됨
- 게임 종료 팝업이 HUD 위에 표시됨

**결과:** PASS

---

### SINGLE-4: 재시작 시 UI 초기화

**전제:** 게임 종료 후 게임 종료 팝업이 표시된 상태

**동작:**
1. 다시하기 버튼을 눌러 게임을 재시작함

**기댓값:**
- 게임 종료 팝업이 닫힘
- 생산 패널/건물 배치 팝업이 모두 닫힌 초기 상태로 시작됨
- HUD가 초기 수치(골드, 인구수)를 정상적으로 표시함

**결과:** PASS

---

### SINGLE-5: 게임 진행 중 팝업 없는 상태에서 게임 종료

**전제:** 게임이 진행 중이고, 생산 패널/건물 배치 팝업이 모두 닫혀 있는 상태

**동작:**
1. 게임이 종료됨

**기댓값:**
- 오류 없이 게임 종료 팝업이 정상 표시됨
- 콘솔에 에러 없음

**결과:** PASS

---

### MULTI-1: 멀티플레이 게임 종료 시 팝업 자동 닫힘

**전제:** 멀티플레이(Host + Client) 게임 진행 중, 양측 모두 생산 패널이 열려 있는 상태

**동작:**
1. 한쪽 캐슬이 파괴되어 게임이 종료됨

**기댓값:**
- Host와 Client 양측 모두 생산 패널이 닫힘
- 각자 올바른 승리/패배 팝업이 표시됨

**결과:** PASS — 실기 테스트 완료 (BUG-1 수정 후 Host/Client 양측 정상 동작 확인)

---

## QA 섹션 (qa-tester 에이전트 전용)

### 정적 분석 대상 파일
- `Assets/_Project/Scripts/Presentation/UI/Core/IGameUI.cs`
- `Assets/_Project/Scripts/Presentation/UI/GameUIManager.cs`
- `Assets/_Project/Scripts/Application/Events/GameEvents.cs` (OnGameStarted 등 추가 Subject)
- `Assets/_Project/Scripts/Presentation/UI/GameHudUI.cs`
- `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`
- `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`
- `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

---

## 정적 분석 결과 (qa-tester)

### 분석 요약

#### 1. IGameUI default 구현 (C# 8+ 문법)
- **판정: PASS**
- `void OnGameStarted() { }`, `void OnGameEnded() { }` 등 모든 메서드에 인터페이스 레벨 default 빈 구현이 올바르게 작성됨.
- Unity 6은 .NET Standard 2.1 / C# 9 이상 환경으로 default interface member 문법 지원.

#### 2. GameEndUI 제외 로직
- **판정: PASS**
- `GameUIManager.NotifyGameEnded()`에서 `ReferenceEquals(ui, _gameEndUI)` 비교로 동일 인스턴스를 정확히 식별하여 제외.
- `_gameEndUI == null` 체크를 먼저 수행하므로 null 참조 안전.
- `_gameEndUI`가 Inspector에 연결되지 않은 경우 제외 조건이 `false`가 되어 GameEndUI에도 `OnGameEnded()`가 호출될 수 있음. 단, GameEndUI의 `OnGameEnded()`는 IGameUI default 빈 구현을 사용하므로 실질적 오동작 없음.
- **단, Inspector 미연결 시 의도와 다른 호출 흐름이 됨 — Minor 이슈로 분류.**

#### 3. Register/Initialize 호출 위치 (UseCase 주입 이전/이후)
- **판정: PASS**
- `LoadMap()` 내 실행 순서:
  - 맨 처음: `_uiManager.Register(...)` + `_uiManager.Initialize()` 호출 (이벤트 구독만 설정)
  - step 4: `CreateUseCases()` (UseCase 생성)
  - step 10: `_gameHudUI.Initialize(...)` (UseCase 주입)
  - step 10-1: `_gameEndUI.Initialize()` (이벤트 구독 설정)
- `GameUIManager.Initialize()`는 이벤트 구독만 설정하며 UseCase를 직접 사용하지 않으므로, UseCase 생성 이전에 호출되어도 문제없음.
- `GameEvents.OnGameStarted`는 step 14(LoadMap 마지막)에서 발행되므로, 이 시점에 모든 UseCase 주입과 UI 초기화가 완료된 상태임.

#### 4. OnGameStarted 발행 타이밍
- **판정: PASS**
- `GameEvents.OnGameStarted.OnNext(Unit.Default)`는 `LoadMap()`의 step 14로 맨 마지막에 위치.
- step 10 (HUD 초기화), step 10-1 (GameEndUI 초기화), step 9 (생산 시스템 초기화) 등 모든 UI 및 UseCase 초기화가 완료된 이후 발행.
- `GameEvents.OnGameEnd`에 대한 GameEndUI 자체 구독도 step 10-1 `_gameEndUI.Initialize()`에서 이미 등록된 상태.

#### 5. UniRx Subject<Unit> 타입 충돌 여부
- **판정: PASS**
- `GameEvents.cs`의 `Subject<Unit>`, `GameBootstrapper.cs`의 `Unit.Default` 모두 `UniRx.Unit` 타입.
- `Unity.Netcode` 패키지(com.unity.netcode.gameobjects)에는 `Unit` 타입이 존재하지 않음을 패키지 소스 전수 검색으로 확인.
- `GameBootstrapper.cs`는 `using Unity.Netcode` + `using UniRx` 동시 선언이나, `Unit`은 `UniRx` 네임스페이스에만 존재하므로 모호성(CS0104) 없음.
- 프로젝트 내 `Unit.Default` 사용 패턴이 Lobby 뷰 파일들에서도 이미 동일하게 사용되고 있어 기존에 검증된 패턴.

#### 6. 구독 중복 방지 (CompositeDisposable)
- **판정: PASS**
- `GameUIManager.Initialize()`: `_subscriptions?.Dispose()` 후 `new CompositeDisposable()` 재생성. 재호출 시 이전 구독이 먼저 정리됨.
- `GameEndUI.Initialize()`: `_gameEndSubscription?.Dispose()` 후 재구독. 중복 방지 동일 패턴.
- `OnDestroy()`에서 각자 `_subscriptions?.Dispose()` / `_gameEndSubscription?.Dispose()` 호출로 씬 전환 시 메모리 누수 방지.

#### 7. IGameUI 구현 현황 요약

| UI 클래스 | OnGameEnded() | OnGameStarted() | 비고 |
|-----------|--------------|----------------|------|
| GameHudUI | default (빈 구현) | 캐시값 초기화 | HUD는 종료 시에도 유지 — 올바른 설계 |
| ProductionPanelUI | Close() 호출 | Close() 호출 | 정상 |
| BuildingPlacementUI | Close() 호출 | Close() 호출 | 정상 |
| GameEndUI | default (빈 구현) | Hide() 호출 | GameUIManager에서 제외 대상 — 의도에 맞는 설계 |

---

### SINGLE-1 정적 분석 판정
- `GameEvents.OnGameEnd` 발행 → `GameUIManager.NotifyGameEnded()` → `ProductionPanelUI.OnGameEnded()` → `Close()` 실행 흐름 추적됨.
- `GameEndUI.Initialize()`에서 `GameEvents.OnGameEnd`를 직접 구독 → `ShowResult` 또는 `OnGameEnd` 핸들러가 패널 표시.
- **판정: CONDITIONAL PASS** — 코드 흐름 상 정상이나 `_uiManager._gameEndUI` Inspector 연결 상태 실기 확인 필요.

### SINGLE-2 정적 분석 판정
- `GameEvents.OnGameEnd` → `BuildingPlacementUI.OnGameEnded()` → `Close()` 흐름 정상.
- **판정: CONDITIONAL PASS** — 실기 확인 필요.

### SINGLE-3 정적 분석 판정
- `GameHudUI.OnGameEnded()`는 default 빈 구현 — HUD를 닫거나 숨기는 코드 없음.
- `GameHudUI`는 `GameUIManager`에 등록되어 있으나 `OnGameEnded()`가 아무것도 하지 않으므로 게임 종료 후에도 HUD 그대로 유지됨.
- **판정: CONDITIONAL PASS** — 실기 확인 필요.

### SINGLE-4 정적 분석 판정
- 재시작 시 `LoadMap()` 재호출 → `GameEvents.OnGameStarted.OnNext()` 발행 → `GameUIManager.NotifyGameStarted()` → 등록된 모든 UI `OnGameStarted()` 호출.
- `ProductionPanelUI.OnGameStarted()` → `Close()`, `BuildingPlacementUI.OnGameStarted()` → `Close()`, `GameEndUI.OnGameStarted()` → `Hide()`, `GameHudUI.OnGameStarted()` → 캐시 초기화(다음 Update에서 새 값 표시).
- **판정: CONDITIONAL PASS** — 실기 확인 필요.

### SINGLE-5 정적 분석 판정
- `ProductionPanelUI.Close()`, `BuildingPlacementUI.Close()` 모두 `_popup?.Hide()` 내부에서 AnimatedPanel null-safe 호출.
- 팝업이 이미 닫혀있는 상태에서 `Close()` 재호출 시 AnimatedPanel.Hide()가 중복 호출되는 구조이나, DOTween `?.Kill()` 처리가 되어있으면 무해 (AnimatedPanel 구현 별도 확인 불필요 — 이미 DOTween 프레임워크 QA에서 검증 완료).
- **판정: CONDITIONAL PASS** — 실기 확인 필요.

### MULTI-1
- **에이전트 실기 불가 — 사용자 확인 필요**
- 멀티플레이에서는 `GameEndUI.ShowResult()` 경로(NetworkGameEndController → GameEndUI)를 통해 결과가 표시됨. `GameEvents.OnGameEnd` 직접 구독 경로와 별도임을 확인.
- `GameUIManager.NotifyGameEnded()`는 Host/Client 양측에서 각자 호출됨 (GameEvents.OnGameEnd는 로컬 이벤트 버스이므로). 서버에서 클라이언트에 RPC로 전파하는 구조가 NetworkGameEndController에서 처리됨 — 이 부분은 기존 구현 유지.

---

### 발견된 이슈

| 심각도 | 설명 | 관련 파일 |
|--------|------|----------|
| Minor | `GameUIManager._gameEndUI` Inspector 미연결 시 GameEndUI에도 `OnGameEnded()` 호출됨. 현재 구현에서는 실질적 오동작이 없으나(default 빈 구현 사용), 의도와 다른 흐름. Inspector 연결 필수 확인 필요. | `GameUIManager.cs` L50, L154 |

### 종합 판정: CONDITIONAL PASS

정적 분석 결과 컴파일 에러 및 로직 버그는 발견되지 않았다. 모든 TC는 코드 흐름 상 기댓값을 충족하나, 실기 실행 시 Inspector 연결 상태(특히 `GameUIManager._gameEndUI`) 확인이 필요하다.
