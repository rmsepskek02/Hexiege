# Plan — 랜덤 매칭 후 종족 캐릭터 잘못 표시 버그

## 작업 개요

랜덤 매칭이 완료된 직후 로비의 종족 선택 캐러셀에서 내가 선택한 캐릭터가 다른 종족 캐릭터로 잠깐 바뀌어 보이는 버그를 수정한다.
코드 분석만으로는 발생 경로를 100% 확정할 수 없어, **1단계: 로그 추가 → 2단계: 버그 수정** 순서로 진행한다.

> **이 작업은 GameSystemRules.md의 유닛/전투 규칙과 직접 연관이 없다.**
> (로비 UI 및 네트워크 연결 흐름에 관한 작업)

---

## 1단계: 런타임 로그 추가 (원인 추적용)

코드 분석에서 파악한 의심 지점 4곳에 `Debug.Log`를 추가하여, 버그 재현 시 어떤 경로로 문제가 발생했는지 로그로 확인한다.

### 1-1. `BattleViewModel.cs` — CurrentScreen 변경 감시

**목적**: 매칭 중 `CurrentScreen`이 의도치 않게 `Main`으로 바뀌는지 확인

**수정 위치**: `CmdStartMatchmaking` 핸들러의 각 catch 블록, `CurrentScreen.Value` 변경 시점

추가할 로그:
```csharp
// CurrentScreen.Value = BattleScreen.Main 직전
Debug.Log($"[BugTrace] CurrentScreen → Main. 경로: OperationCanceledException. 시각: {System.DateTime.Now:HH:mm:ss.fff}");

// CurrentScreen.Value = BattleScreen.RandomMatch 직후  
Debug.Log($"[BugTrace] CurrentScreen → RandomMatch. 매칭 시작. 시각: {System.DateTime.Now:HH:mm:ss.fff}");

// OnClientConnected()
Debug.Log($"[BugTrace] OnClientConnected 호출. ConnectedPlayers: {ConnectedPlayers.Value + 1}. 시각: {System.DateTime.Now:HH:mm:ss.fff}");
```

---

### 1-2. `RaceSelectionViewModel.cs` — 생성 시점과 초기 종족값 감시

**목적**: `BattleRootView.Bind()` 재호출 여부, 그 시점의 `LocalPlayerRace.Current` 값 확인

**수정 위치**: 생성자 진입부

추가할 로그:
```csharp
// 생성자 첫 줄
Debug.Log($"[BugTrace] RaceSelectionViewModel 생성. LocalPlayerRace.Current: {LocalPlayerRace.Current}. 시각: {System.DateTime.Now:HH:mm:ss.fff}");
```

---

### 1-3. `RaceSelectionView.cs` — 캐릭터 위치 변경 감시

**목적**: `ApplyCarouselPositions()`가 언제, 어떤 종족값으로 호출되는지 확인

**수정 위치**: `ApplyCarouselPositions()` 진입부

추가할 로그:
```csharp
// ApplyCarouselPositions() 첫 줄
Debug.Log($"[BugTrace] ApplyCarouselPositions 호출. race: {selected}, animate: {animate}. 시각: {System.DateTime.Now:HH:mm:ss.fff}");
```

---

### 1-4. `BattleRootView.cs` — Bind/Unbind 호출 감시

**목적**: 씬 초기화 외에 `Bind()`가 추가로 호출되는지 확인

**수정 위치**: `Bind()` 진입부, `Unbind()` 진입부

추가할 로그:
```csharp
// Bind() 첫 줄
Debug.Log($"[BugTrace] BattleRootView.Bind() 호출. 시각: {System.DateTime.Now:HH:mm:ss.fff}");

// Unbind() 첫 줄
Debug.Log($"[BugTrace] BattleRootView.Unbind() 호출. 시각: {System.DateTime.Now:HH:mm:ss.fff}");
```

---

## 수정할 파일 목록 (1단계)

| 파일 | 수정 내용 |
|------|-----------|
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` | CurrentScreen 변경·OnClientConnected 로그 추가 |
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/RaceSelectionViewModel.cs` | 생성자 로그 추가 |
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs` | ApplyCarouselPositions 로그 추가 |
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleRootView.cs` | Bind/Unbind 로그 추가 |

---

## 2단계: 버그 수정 (로그 확인 후 진행)

사용자가 버그를 재현하고 로그를 공유한 후에 이 단계를 진행한다. 확인된 원인에 따라 다음 중 하나를 적용한다.

### 예상 수정 경로 A — CurrentScreen이 의도치 않게 Main으로 변경

**원인**: 매칭 완료 직후 어떤 경로로 `catch (OperationCanceledException)` 블록에 진입하여 `CurrentScreen = Main`이 되는 경우

**수정 방향**: 
- `CmdStartMatchmaking`의 `OperationCanceledException` catch 블록에 `IsMatchmaking.Value`가 false인 경우(= 사용자가 명시적으로 취소한 경우)에만 `CurrentScreen = Main`으로 변경하도록 조건 추가
- 또는 매칭 완료 후 씬 전환이 확정된 이후에는 `CurrentScreen` 변경을 차단

**수정 파일**: `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs`

---

### 예상 수정 경로 B — RaceSelectionView.Bind()가 예기치 않게 재호출

**원인**: `BattleRootView.Bind()`가 씬 초기화 외 타이밍에 재호출되어 `LocalPlayerRace.Current` 기본값(Human)으로 캐릭터 재배치

**수정 방향**:
- `BattleRootView.Bind()` 내에서 이미 바인딩된 상태라면 재바인딩 방지 플래그 추가
- 또는 `RaceSelectionViewModel` 재생성 시 `LocalPlayerRace.Current` 대신 기존 `_raceVm.SelectedRace.Value`를 유지

**수정 파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleRootView.cs`

---

### 예상 수정 경로 C — _rawImage가 BattleMainView 외부에 연결

**원인**: 캐러셀의 `_rawImage`(RenderTexture 출력)가 `BattleMainView.SetActive(false)`의 영향을 받지 않는 위치에 있어 매칭 대기 중에도 항상 보임

**수정 방향**: Inspector에서 `_rawImage`를 `BattleMainView` 계층 안으로 이동하거나, `CurrentScreen` 변경 시 `_rawImage.gameObject.SetActive()`를 명시적으로 처리

**수정 파일**: Inspector 수정 (에디터 스크립트 불필요, 사용자가 직접 계층 이동)

---

## 위험 요소

| 위험 | 내용 |
|------|------|
| 로그 과다 | `ApplyCarouselPositions` 로그는 매 종족 변경마다 출력되므로, 초기 배치 시 3회 연속 출력됨 — 정상 |
| UniRx Subscribe 즉시 발행 | `RaceSelectionViewModel` 생성 직후 구독 시 현재 값이 즉시 발행되어 `ApplyCarouselPositions`가 호출됨 — 정상 동작, 로그 기준점으로 활용 |
| 로그 순서 불일치 | Unity 멀티스레딩에 의해 로그 시각이 실제 실행 순서와 다를 수 있음 — `[BugTrace]` 태그로 필터링하여 확인 |

---

## 테스트 방법

1. 로그 추가 후 빌드 (또는 에디터 + 빌드 멀티플레이 구성)
2. A(Human), B(Spirit)로 랜덤 매칭 진행
3. 버그가 발생하는 시점에 Console 로그를 캡처
4. `[BugTrace]` 태그로 필터링하여 다음 항목 확인:
   - `CurrentScreen → Main`이 의도치 않게 출력되었는지
   - `RaceSelectionViewModel 생성`이 씬 초기화 외 시점에 출력되었는지
   - `ApplyCarouselPositions` 호출 시 `race`가 Human인 타이밍이 언제인지
5. 로그를 채팅으로 공유 → 원인 확정 후 2단계 진행

---

## 작업 순서

```
[1] game-programmer에게 1단계 로그 추가 구현 요청
[2] 사용자가 빌드 후 버그 재현 → 로그 캡처 → 공유
[3] 로그 기반 원인 확정
[4] 원인에 맞는 수정 경로(A/B/C) 선택 → game-programmer에게 2단계 수정 요청
[5] 수정 후 재테스트
[6] 로그 코드 제거 (1단계 추가분)
```
