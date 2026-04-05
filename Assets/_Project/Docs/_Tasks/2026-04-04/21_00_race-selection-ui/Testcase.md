# Testcase: 로비 종족 선택 UI

## TC 목록

---

### SINGLE-1: 종족 선택 UI 표시

**전제:** 로비 씬 전투 탭 메인 화면이 열려 있다.

**동작:**
1. 전투 탭을 누른다.

**기댓값:**
- 랜덤 매칭 버튼 하단에 종족 선택 UI가 표시된다.
- 기본 선택 종족은 "인간"이며 대표 캐릭터(임시 오브젝트)가 RawImage 영역에 보인다.
- 종족명 텍스트에 "인간"이 표시된다.

**결과:** PASS

---

### SINGLE-2: 오른쪽 화살표로 종족 순환

**전제:** 종족 선택 UI가 표시된 상태. 현재 선택 종족은 "인간".

**동작:**
1. 오른쪽 화살표 버튼을 누른다.
2. 다시 오른쪽 화살표 버튼을 누른다.
3. 다시 오른쪽 화살표 버튼을 누른다.

**기댓값:**
- 1회: "정령" 캐릭터 오브젝트가 표시되고 종족명 텍스트가 "정령"으로 변경된다.
- 2회: "초월" 캐릭터 오브젝트가 표시되고 종족명 텍스트가 "초월"으로 변경된다.
- 3회: 다시 "인간"으로 순환된다.

**결과:** PASS

---

### SINGLE-3: 왼쪽 화살표로 종족 역방향 순환

**전제:** 종족 선택 UI가 표시된 상태. 현재 선택 종족은 "인간".

**동작:**
1. 왼쪽 화살표 버튼을 누른다.

**기댓값:**
- "초월" 캐릭터 오브젝트가 표시되고 종족명 텍스트가 "초월"으로 변경된다 (역방향 순환).

**결과:** PASS

---

### SINGLE-4: 종족 선택 후 싱글플레이 진입 — 선택 종족 유지

**전제:** 로비에서 "정령" 종족을 선택한 상태.

**동작:**
1. 싱글플레이 버튼을 누른다.
2. 게임 씬이 로드된다.

**기댓값:**
- 게임이 정상적으로 시작된다 (오류 없음).
- (다음 작업에서 확인 예정) 게임 내에서 정령 종족 유닛/건물이 적용된다.

**결과:** 시작은 되지만 아직 종족반영이 되지 않음 다음 작업 예정

---

### SINGLE-5: 전투 탭이 아닌 다른 탭에서 종족 선택 UI 비표시

**전제:** 로비 씬이 열려 있다.

**동작:**
1. 상점 탭을 누른다.
2. 다시 전투 탭을 누른다.

**기댓값:**
- 상점 탭에서는 종족 선택 UI가 표시되지 않는다.
- 전투 탭으로 돌아오면 종족 선택 UI가 다시 표시된다.
- 이전에 선택했던 종족이 유지된다.

**결과:** PASS

---

### SINGLE-6: 커스텀 게임 화면 전환 시 종족 선택 UI 비표시

**전제:** 로비 전투 탭 메인 화면. 종족이 "정령"으로 선택된 상태.

**동작:**
1. 커스텀 게임 버튼을 누른다.
2. 뒤로가기 버튼을 누른다.

**기댓값:**
- 커스텀 게임 화면으로 전환되면 BattleMainView 전체(종족 선택 UI 포함)가 비활성화된다.
- 메인 화면으로 돌아오면 종족 선택 UI가 다시 표시되며 "정령"이 유지된다.

**결과:** 해당 내용 변경, 항상 표시 TC 제거 바람

---

### MULTI-1: 멀티플레이 — 양 플레이어 종족이 게임 시작 시 전달됨

**전제:** Host(인간 선택)와 Client(초월 선택)가 각각 로비에서 종족을 선택한 상태.

**동작:**
1. Host가 랜덤 매칭 또는 커스텀 게임으로 방을 만든다.
2. Client가 방에 접속한다.
3. 게임이 시작된다.

**기댓값:**
- 게임이 정상적으로 시작된다 (오류 없음).
- (다음 작업에서 확인 예정) Host: 인간 종족, Client: 초월 종족으로 게임이 진행된다.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

## QA 섹션

### 정적 분석 결과

#### 검토 항목
- `RaceId.cs`: Domain 레이어, 외부 의존성 없음
- `LocalPlayerRace.cs`: Infrastructure 레이어, LocalPlayerTeam과 동일 패턴
- `GameRaceContext.cs`: Infrastructure 레이어, BlueRace/RedRace 저장
- `RaceSelectionViewModel.cs`: 순수 C#, UniRx 사용, IDisposable 구현
- `RaceSelectionView.cs`: MonoBehaviour, IView 패턴 준수
- `BattleMainView.cs`: IView 인터페이스 시그니처 유지, BindRace 별도 메서드
- `BattleRootView.cs`: RaceSelectionViewModel 생성/Dispose 책임 올바름
- `NetworkGameFlow.cs`: RaceId를 int로 직렬화, GameRaceContext.Set 호출

#### 잠재적 위험 요소
- `_characterRoots` 배열 Inspector 미연결 시 → null 방어 코드 확인 필요
- `NetworkGameFlow.RequestReadyServerRpc`에서 Blue/Red 판별: `SenderClientId == NetworkManager.ServerClientId` 정확도 확인 필요
- `RaceId` enum이 NGO ServerRpc 파라미터로 직접 직렬화 가능한지 → int 캐스팅으로 안전 처리됨

---

## 정적 분석 결과 (qa-tester)

### 분석 대상 파일

| 파일 | 역할 |
|------|------|
| `RaceId.cs` | Domain — 종족 열거형 |
| `LocalPlayerRace.cs` | Infrastructure — 로컬 플레이어 종족 전역 홀더 |
| `GameRaceContext.cs` | Infrastructure — 양 팀 종족 전역 홀더 |
| `RaceSelectionViewModel.cs` | Presentation ViewModel — 순수 C#, UniRx |
| `RaceSelectionView.cs` | Presentation View — MonoBehaviour |
| `BattleMainView.cs` | Presentation View — BattleViewModel + BindRace 분리 |
| `BattleRootView.cs` | Presentation — ViewModel 생성/Dispose 책임 |
| `NetworkGameFlow.cs` | Infrastructure — ServerRpc 종족 수신, GameRaceContext 설정 |

---

### TC 판정

#### SINGLE-1: 종족 선택 UI 표시

**정적 분석:**
- `BattleRootView.Bind()` 에서 `_raceVm = new RaceSelectionViewModel()` 생성 후 `_battleMainView.BindRace(_raceVm)` 호출 — 순서 정상.
- `RaceSelectionViewModel` 생성자에서 `SelectedRace` 초기값이 `RaceId.Human`이고, `SelectedRaceName`은 `Select` 파이프라인으로 "인간"을 반환.
- `RaceSelectionView.Bind()` 에서 `SelectedRace` 구독 직후 초기값으로 인덱스 0번 캐릭터 활성화, 나머지 비활성화.
- `_characterRoots` 배열이 비어 있거나 null이면 null 방어 코드로 조용히 건너뜀 (L81).

**결과:** CONDITIONAL PASS — 에이전트 실기 불가 — 사용자 확인 필요 (Inspector `_characterRoots[0]` 연결 여부)

---

#### SINGLE-2: 오른쪽 화살표로 종족 순환

**정적 분석:**
- `CmdNext` → `(current + 1) % 3` 계산: 0→1→2→0 순환. Human→Spirit→Nature→Human 순서와 일치.
- `_nextButton.onClick`에 `vm.CmdNext.OnNext(Unit.Default)` 연결 — 정상.
- `SelectedRace` 변경 시 `SelectedRaceName` 자동 갱신, `_characterRoots` 활성화 전환 모두 구독으로 처리.

**결과:** CONDITIONAL PASS — 에이전트 실기 불가 — 사용자 확인 필요

---

#### SINGLE-3: 왼쪽 화살표로 역방향 순환

**정적 분석:**
- `CmdPrev` → `(current + 3 - 1) % 3` 계산: 0→2(초월) 순환. Human에서 왼쪽 → Nature 기댓값과 일치.
- 음수 나머지 방지 처리 정상 (`+RaceCount-1` 패턴).

**결과:** CONDITIONAL PASS — 에이전트 실기 불가 — 사용자 확인 필요

---

#### SINGLE-4: 종족 선택 후 싱글플레이 진입 — 선택 종족 유지

**정적 분석:**
- `RaceSelectionViewModel` 생성자에서 `SelectedRace.Subscribe(race => LocalPlayerRace.Set(race))` — 초기값 Human 포함 모든 변경이 `LocalPlayerRace`에 즉시 저장됨.
- 주석(L110)에는 "Skip(1): 초기값 건너뜀"이라고 적혀 있으나, **실제 코드에는 Skip(1)이 없음** — 주석과 코드 불일치. (기능상 초기값도 저장되므로 동작에는 문제없으나 주석이 오해를 유발함) — BUG-1

**[BUG-1] Minor — 주석-코드 불일치**
- 위치: `RaceSelectionViewModel.cs` L110
- 내용: 주석에 "Skip(1): 초기값(Human) 설정은 건너뜀"이라 적혀 있으나, 실제 파이프라인에 `Skip(1)` 연산자 없음. 초기값 Human이 `LocalPlayerRace.Set(Human)` 호출되므로 동작 자체는 정상. 주석이 잘못된 구현 의도를 서술하고 있어 추후 혼란 유발 가능.
- 수정 방향: 주석을 "초기값 포함 모든 변경을 LocalPlayerRace에 저장"으로 수정하거나, 실제로 초기값을 건너뛰려면 `.Skip(1)` 추가.

**[BUG-2] Major — 싱글플레이에서 GameRaceContext.Set() 미호출**
- 위치: `GameBootstrapper.Start()` → `LoadMap()` 경로, `BattleViewModel.LoadSingleplayScene()`
- 내용: `GameRaceContext.cs` 주석에는 "싱글플레이: GameBootstrapper에서 LocalPlayerRace.Current를 Blue로 설정"이라고 명시되어 있으나, `GameBootstrapper.Start()` → `LoadMap()` 전체 경로 어디에도 `GameRaceContext.Set()` 호출이 없음. `BattleViewModel.LoadSingleplayScene()`도 `SceneManager.LoadScene("Game")`만 호출하고 GameRaceContext를 설정하지 않음.
- 영향: 싱글플레이로 게임 시작 시 GameRaceContext.BlueRace가 항상 기본값 Human으로 유지됨. 인게임에서 선택 종족 기반 유닛/건물 풀 초기화가 구현되는 다음 작업부터 버그가 가시화됨.
- 재현 경로: 로비에서 "정령" 선택 → 싱글플레이 버튼 클릭 → 게임 씬 진입 → `GameRaceContext.BlueRace` 값 확인 → Human(0)
- 수정 방향: `BattleViewModel.LoadSingleplayScene()` 또는 `GameBootstrapper.Start()` 내 싱글플레이 분기에서 `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` 호출 추가.

**결과:** FAIL (BUG-2)

---

#### SINGLE-5: 전투 탭이 아닌 다른 탭에서 종족 선택 UI 비표시

**정적 분석:**
- `BattleMainView`는 `vm.CurrentScreen == Main`일 때만 `gameObject.SetActive(true)` — 전투 탭 내 화면 전환은 정상.
- 탭 전환(전투 탭 → 상점 탭)은 `BattleRootView`를 포함하는 상위 View의 `SetActive` 또는 `Bind/Unbind` 로직에 달려 있음.
- `LocalPlayerRace`는 정적 홀더이므로 탭 전환과 무관하게 값 유지됨.
- `RaceSelectionViewModel`은 `BattleRootView.Unbind()` 시 Dispose — 탭 재진입 시 `Bind()` 재호출로 새 인스턴스 생성. 이때 `SelectedRace` 초기값은 항상 Human으로 리셋됨.

**[BUG-3] Major — 탭 재진입 시 선택 종족 초기화**
- 위치: `BattleRootView.Bind()` → `new RaceSelectionViewModel()` — 항상 Human으로 초기화
- 내용: 기댓값(SINGLE-5): "전투 탭으로 돌아오면 이전에 선택했던 종족이 유지된다." 그러나 `Bind()` 재호출 시 `_raceVm = new RaceSelectionViewModel()`으로 새 인스턴스가 생성되므로 `SelectedRace` 초기값이 Human으로 리셋됨. `LocalPlayerRace.Current`는 이전 값이 유지되지만, ViewModel의 `SelectedRace`와 동기화되지 않음 — UI 표시는 Human, 실제 저장값은 유지되는 불일치 발생.
- 수정 방향: `RaceSelectionViewModel` 생성자에서 `LocalPlayerRace.Current`로 초기값을 설정하거나, `BattleRootView`가 ViewModel을 영속 보관하고 Bind/Unbind 시 재사용.

**결과:** FAIL (BUG-3)

---

#### SINGLE-6: 커스텀 게임 화면 전환 시 종족 선택 UI 비표시

**정적 분석:**
- `BattleMainView`는 `vm.CurrentScreen == Main`이 아닐 때 `SetActive(false)` — 커스텀 게임 화면 전환 시 BattleMainView 전체(종족 선택 포함) 비활성화 정상.
- 돌아올 때 `CurrentScreen = Main` 복구 → `SetActive(true)` 재활성화 정상.
- 단, SINGLE-5와 동일하게 같은 `_raceVm` 인스턴스가 유지되므로 탭 전환과 달리 커스텀 화면 전환은 ViewModel 재생성이 없음 → 종족 유지됨.

**결과:** CONDITIONAL PASS — 에이전트 실기 불가 — 사용자 확인 필요

---

#### MULTI-1: 멀티플레이 — 양 플레이어 종족이 게임 시작 시 전달됨

**정적 분석:**
- `RequestReadyServerRpc(int race, ServerRpcParams rpcParams = default)` — NGO 2.9.2에서 `RequireOwnership = false`로 설정되어 있어 누구나 호출 가능.
- `senderId == NetworkManager.ServerClientId` 판별: NGO에서 Host의 ClientId는 항상 `NetworkManager.ServerClientId`(보통 0). Listen Server 구조에서 Host가 ServerRpc를 호출하면 `SenderClientId`는 `NetworkManager.ServerClientId`와 일치. 이 방식은 NGO 2.x 문서 기준 신뢰 가능.
- `int` 파라미터로 직렬화 후 `(RaceId)` 캐스팅 — NGO가 enum 직렬화를 지원하지 않는 경우를 대비한 int 변환 처리 정상.
- `GameRaceContext.Set((RaceId)blueRace, (RaceId)redRace)` — `StartGameClientRpc`에서 모든 클라이언트에 호출되므로 양측 모두 설정됨.
- `_readyCount` 누적 방식 사용 — 동일 클라이언트가 재연결하거나 중복 신호를 보낼 경우 카운트 초과 가능하나, `_gameStarted` 플래그로 중복 시작은 차단. 기존 2명 고정 구조에서 실질적 위험 낮음.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

### 아키텍처 검증 결과

| 항목 | 결과 |
|------|------|
| `RaceId.cs` — Domain 레이어, 외부 의존성 없음 | PASS |
| `LocalPlayerRace.cs` — `using Hexiege.Domain`만 사용 (Core 없음) | PASS |
| `GameRaceContext.cs` — `using Hexiege.Domain`만 사용 (Core 없음) | PASS |
| `NetworkGameFlow.cs` — Infrastructure 레이어에 NetworkBehaviour 배치 | PASS |
| `IView<T>` 인터페이스 시그니처 (`Bind`/`Unbind`) 준수 | PASS |
| `RaceId` → `int` 직렬화 후 ServerRpc 전송 | PASS |
| `BattleMainView.Bind()` + `BindRace()` 분리, `BattleRootView`에서 두 메서드 모두 호출 | PASS |
| `RaceSelectionViewModel.Dispose()` — `_disposables.Dispose()` 호출, 전체 구독 해제 | PASS |
| `BattleRootView.Unbind()` — `_raceVm?.Dispose()` + `_raceVm = null` | PASS |
| `_characterRoots` null/비어있음 방어 코드 존재 | PASS |

---

### 발견된 버그 요약

| BUG-ID | 심각도 | 설명 | 위치 |
|--------|--------|------|------|
| BUG-1 | Minor | `RaceSelectionViewModel.cs` L110 주석에 "Skip(1)" 언급이 있으나 실제 코드에는 없음 — 주석-코드 불일치 | `RaceSelectionViewModel.cs:110` |
| BUG-2 | Major | 싱글플레이 게임 시작 경로에 `GameRaceContext.Set()` 호출 없음 — 선택 종족이 인게임에 전달되지 않음 | `BattleViewModel.LoadSingleplayScene()`, `GameBootstrapper.Start()` |
| BUG-3 | Major | 전투 탭 재진입(Bind 재호출) 시 `RaceSelectionViewModel`이 Human으로 초기화됨 — UI 표시 종족이 선택 이력과 불일치 | `BattleRootView.Bind()` → `new RaceSelectionViewModel()` |

---

### 종합 판정

**FAIL**

BUG-2(싱글플레이 GameRaceContext 미설정)와 BUG-3(탭 재진입 시 종족 초기화)이 기획 요구사항과 직접 충돌합니다. 두 버그 모두 현재 작업 범위에서 수정이 필요합니다. BUG-1은 Minor이나 혼란 방지를 위해 함께 수정 권장합니다.
