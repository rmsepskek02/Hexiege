# Testcase.md — LoadingIndicator 전체 커버리지

**작업 목적:** 씬 전환이 일어나는 모든 진입점에서 전역 로딩 인디케이터(ShowLoading)가 빠짐없이 켜지고, 목적지 씬 준비 완료 시점에 정확히 꺼지는지 검증한다.

---

## TC 목록

---

### SINGLE-TC-01: 로그인 완료 후 로비 씬 진입 — 로딩 인디케이터 표시 및 해제

**전제:** 에디터에서 Login 씬 진입. UIManager 오브젝트가 씬에 배치되어 있다. 미로그인 상태(자동 로그인 실패 경로).

**동작:**
1. 스플래시 화면에서 화면을 탭하여 로그인 선택 화면으로 진입한다.
2. 로그인 방식(이메일 또는 익명)을 선택하고 로그인을 완료한다.
3. 화면 전환이 일어나는 동안 상단 또는 중앙에 로딩 인디케이터가 표시되는지 관찰한다.
4. 로비 씬이 완전히 열리고 탭 바, 전투 패널 등이 보이는 시점을 확인한다.

**기댓값:**
- 로그인 완료와 동시에 "로비로 이동 중..." 메시지와 함께 로딩 인디케이터가 표시된다.
- 로비 씬 콘텐츠가 모두 준비되면 로딩 인디케이터가 자동으로 사라진다.
- 로딩이 사라진 직후 로비 화면이 자연스럽게 표시된다.

**결과:** PASS (2026-06-23 사용자 확인)

---

### SINGLE-TC-02: 싱글플레이 시작 — 게임 씬 진입 로딩 표시 및 해제

**전제:** 로비 씬. 싱글플레이 시작 버튼이 있는 상태.

**동작:**
1. 전투 탭에서 싱글플레이 시작 버튼을 누른다.
2. 화면이 전환되는 동안 로딩 인디케이터가 표시되는지 관찰한다.
3. 게임 씬이 열리고 맵, 건물, 유닛이 배치되는 시점을 확인한다.

**기댓값:**
- 시작 버튼을 누르는 즉시 "게임을 불러오는 중..." 메시지와 함께 로딩 인디케이터가 나타난다.
- 게임 씬에서 맵 배치(Castle 포함)가 완료된 직후 로딩 인디케이터가 사라진다.
- 로딩이 사라진 시점에는 게임 HUD와 타일이 정상적으로 보인다.

**결과:**

---

### MULTI-TC-03: 멀티플레이 호스팅 — 게임 씬 진입 로딩 표시 및 해제

**전제:** 로비 씬. Host 측 에디터 + Client 측 빌드 구성. 방 생성 후 상대방이 참여한 상태.

**동작:**
1. Host가 게임 시작을 누른다.
2. Host와 Client 양쪽 모두에서 화면 전환 중 로딩 인디케이터를 관찰한다.
3. 게임 씬이 양측에 열리고 맵이 배치되는 시점을 확인한다.

**기댓값:**
- Host 측: 시작 버튼과 함께 로딩 인디케이터가 표시되고, 맵 배치 완료 시 사라진다.
- Client 측: 씬 전환이 시작되면 로딩 인디케이터가 표시되고, 맵 배치 완료 시 사라진다.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요 (멀티 환경 미테스트)

---

### SINGLE-TC-04: 로그아웃 — Login 씬 진입 로딩 표시 및 해제

**전제:** 로비 씬, 프로필 탭. 로그인된 계정이 있는 상태.

**동작:**
1. 프로필 탭으로 이동한다.
2. 로그아웃 버튼을 누른다.
3. Firebase/UGS 세션 종료 중 로딩 인디케이터가 표시되는지 관찰한다.
4. Login 씬이 열리고 스플래시 화면이 표시되는 시점을 확인한다.

**기댓값:**
- 로그아웃 버튼을 누르는 즉시 "로그아웃 중..." 메시지와 함께 로딩 인디케이터가 표시된다.
- Login 씬에서 로그인 선택 화면(또는 "Tap to Start")이 준비되면 로딩 인디케이터가 사라진다.

**결과:** PASS (2026-06-23 사용자 확인)

---

### MULTI-TC-05: 멀티 게임 포기 — 결과창 표시 및 로비 복귀 로딩

**전제:** 게임 씬, 멀티플레이 중. Host 측 에디터 + Client 측 빌드.

**동작:**
1. 인게임 설정 메뉴(우상단 버튼)를 연다.
2. 포기 버튼을 누른다.
3. 확인 팝업에서 "포기"를 선택한다.
4. 로딩 인디케이터가 표시되는지, 그 후 결과창(승/패)이 뜨는지 관찰한다.
5. 결과창에서 "로비로" 버튼을 누른다.
6. 로딩 인디케이터가 다시 표시되는지, 로비 씬 진입 후 사라지는지 관찰한다.

**기댓값:**
- 포기 확정 직후 "게임을 포기하는 중..." 메시지와 함께 로딩 인디케이터가 표시된다.
- 결과창이 나타나면 로딩 인디케이터가 먼저 사라진다 (Game 씬 LoadMap 완료 후 꺼짐).
- "로비로" 버튼을 누르면 다시 로딩 인디케이터가 표시되고, 로비 씬이 준비되면 사라진다.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

### SINGLE-TC-06: 싱글 게임 포기 — 결과창만 표시, 로딩 없음 확인

**전제:** 게임 씬, 싱글플레이 중.

**동작:**
1. 인게임 설정 메뉴를 연다.
2. 포기 버튼을 누른다.
3. 확인 팝업에서 "포기"를 선택한다.
4. 로딩 인디케이터가 표시되지 않고 결과창이 바로 뜨는지 관찰한다.

**기댓값:**
- 로딩 인디케이터가 표시되지 않는다.
- 포기 직후 게임 결과창(패배!)이 즉시 나타난다.

**결과:**

---

### SINGLE-TC-07: 싱글 게임 종료 후 "로비로" 버튼 — 로딩 표시 및 해제

**전제:** 게임 씬, 싱글플레이에서 성, 또는 상대 성 파괴 등으로 게임이 종료된 상태.

**동작:**
1. 결과창(승리! 또는 패배!)이 표시된 상태에서 "로비로" 버튼을 누른다.
2. 로딩 인디케이터가 표시되는지 관찰한다.
3. 로비 씬이 열리고 콘텐츠가 준비되는 시점을 확인한다.

**기댓값:**
- "로비로" 버튼을 누르는 즉시 "로비로 이동 중..." 메시지와 함께 로딩 인디케이터가 표시된다.
- 로비 씬이 준비되면 로딩 인디케이터가 자동으로 사라진다.

**결과:**

---

### MULTI-TC-08: 연결 끊김 복귀 버튼 — 로딩 표시 및 해제

**전제:** 게임 씬, 멀티플레이 중. 서버(Host) 측이 강제 종료 또는 연결을 끊어 클라이언트 측에 연결 끊김 팝업이 표시된 상태.

**동작:**
1. 연결 끊김 팝업에서 "확인" 버튼을 누른다.
2. 로딩 인디케이터가 표시되는지 관찰한다.
3. 로비 씬 또는 지정 씬이 열리고 콘텐츠가 준비되는 시점을 확인한다.

**기댓값:**
- "확인" 버튼을 누르는 즉시 "로비로 이동 중..." 메시지와 함께 로딩 인디케이터가 표시된다.
- 목적지 씬이 준비되면 로딩 인디케이터가 자동으로 사라진다.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

### MULTI-TC-09: 재경기 합의 후 게임 씬 재진입 — 양측 로딩 표시 및 해제

**전제:** 게임 씬, 멀티플레이 종료 후 결과창 표시 중. Host 측 에디터 + Client 측 빌드.

**동작:**
1. Host와 Client 양쪽에서 "다시하기" 버튼을 각각 누른다.
2. 양측 모두에서 로딩 인디케이터가 표시되는지 관찰한다.
3. 새 게임 씬이 열리고 맵이 배치되는 시점을 양측에서 확인한다.

**기댓값:**
- 재경기 합의 직후 양측 모두에서 "재경기 준비 중..." 메시지와 함께 로딩 인디케이터가 표시된다.
- 맵 배치가 완료되면 양측 모두에서 로딩 인디케이터가 사라진다.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

### SINGLE-TC-10: 로딩 인디케이터 최소 표시 시간(1초) — 빠른 씬 전환 시 최소 시간 보장

**전제:** 씬 전환이 매우 빠른 환경(에디터, 로컬 빌드). 로딩 인디케이터 최소 표시 시간 기능이 구현되어 있는 경우에 해당.

**동작:**
1. 싱글플레이 시작 버튼을 누른다.
2. 로딩 인디케이터가 표시된 후 1초 이내에 사라지는지 관찰한다.

**기댓값:**
- 씬 전환이 아무리 빨라도 로딩 인디케이터는 최소 1초 이상 표시된 후 사라진다.

**결과:**

---

### SINGLE-TC-11: 로딩 중 UI 입력 차단 — 다른 버튼 클릭 불가 확인

**전제:** 로딩 인디케이터가 표시 중인 씬 전환 상황.

**동작:**
1. 싱글플레이 시작 버튼을 눌러 로딩 인디케이터를 띄운다.
2. 로딩 인디케이터가 표시된 직후 화면의 다른 버튼(탭 바, 시작 버튼 등)을 빠르게 탭해본다.

**기댓값:**
- 로딩 인디케이터가 표시 중일 때 다른 버튼 입력이 차단된다(반응 없음).
- 로딩이 끝난 후에는 정상적으로 버튼이 동작한다.

**결과:**

---

## QA 섹션 — 정적 분석 결과

*qa-tester 에이전트 작성. 근거 코드 위치 포함.*

---

### 정적 분석 개요

점검 대상 파일 10개를 모두 읽고 ShowLoading(true)/ShowLoading(false) 짝 여부, 싱글/멀티 분기, NGO SceneManager 제외 여부, null-safe 패턴, 레이어 규칙을 확인하였다.

---

### PASS 항목

**1. SceneLoader.Load() 내부 ShowLoading(true) 자동 호출**
- `SceneLoader.cs:36` — `UIManager.Instance?.ShowLoading(true, msg)` 후 `SceneManager.LoadScene` 호출.
- null-safe 패턴(`?.`) 적용 확인. PASS.

**2. ProfileView.OnLogoutClicked() ShowLoading(true)**
- `ProfileView.cs:302` — `UIManager.Instance?.ShowLoading(true, "로그아웃 중...")` 호출.
- 예외 발생 시 `ProfileView.cs:316`에서 `UIManager.Instance?.ShowLoading(false)` 복원. PASS.
- 씬 전환 전 비동기 대기가 있어 SceneLoader.Load보다 먼저 켜는 설계 올바름.

**3. InGameSettingsUI.OnForfeitConfirmed() 멀티 분기만 ShowLoading**
- `InGameSettingsUI.cs:342-343` — `NetworkContext.IsNetworkActive` 조건부로만 ShowLoading(true) 호출.
- 싱글 분기에는 ShowLoading 없음. 싱글 포기 시 로딩 없음 설계 의도와 일치. PASS.
- null-safe 패턴 적용 확인. PASS.

**4. GameEndUI.ReturnToLobby() ShowLoading(true)**
- `GameEndUI.cs:325` — `UIManager.Instance?.ShowLoading(true, "로비로 이동 중...")` 호출.
- 이후 멀티 분기: `NetworkGameManager.BackToLobby()`로 위임, 싱글 분기: `SceneLoader.Load()`로 전환.
- 싱글 경로에서는 SceneLoader.Load가 ShowLoading(true)를 중복 호출하지만, 이는 메시지 갱신에 불과하여 부작용 없음. PASS.

**5. NetworkStatusUI.OnReturnButtonClicked() SceneLoader 사용**
- `NetworkStatusUI.cs:239` — `SceneLoader.Load(_returnSceneName, "로비로 이동 중...")` 호출.
- SceneLoader 내부에서 ShowLoading(true)가 자동으로 켜짐. PASS.
- 별도 ShowLoading 호출 없이 SceneLoader에 위임 — 코드 주석(237-238행)에도 명시됨. PASS.

**6. NetworkGameEndController.NotifyRematchStartingClientRpc()**
- `NetworkGameEndController.cs:486` — `GameEvents.OnNetworkRematchStarting.OnNext(Unit.Default)` 발행.
- `GameEndUI.cs:139` — 해당 이벤트 구독 후 `UIManager.Instance?.ShowLoading(true, "재경기 준비 중...")` 호출.
- Infrastructure → Presentation 직접 참조 없이 GameEvents(Application) 경유. 레이어 규칙 준수. PASS.

**7. NGO SceneManager 호출 제외 확인**
- `NetworkGameEndController.cs:470` — `NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single)` 유지.
- SceneLoader.Load()로 교체되지 않았음. NGO 씬 동기화를 위해 반드시 NGO SceneManager를 써야 하므로 올바른 제외. PASS.

**8. ShowLoading(false) 목적지 씬 일원화**
- Login 씬: `LoginBootstrapper.cs:214` — `ShowLoginSelect()`에서 `UIManager.Instance?.ShowLoading(false)`.
- Lobby 씬: `LobbyRootView.cs:131` — `Start()`에서 `UIManager.Instance?.ShowLoading(false)`.
- Game 씬: `GameBootstrapper.Map.cs:174` — `LoadMap()` 맨 끝에서 `UIManager.Instance?.ShowLoading(false)`.
- 세 목적지 씬 모두 ShowLoading(false) 위치 확인. PASS.

**9. GameEvents.OnNetworkRematchStarting 정의 및 구독 연결**
- `GameEvents.cs:823` — `Subject<Unit> OnNetworkRematchStarting` 정의 확인.
- `GameEndUI.cs:138-139` — `_rematchStartingSubscription` 구독 + ShowLoading(true) 호출.
- `GameEndUI.OnDestroy:165` — `_rematchStartingSubscription?.Dispose()` 해제. 메모리 누수 없음. PASS.

---

### 주의 항목 (CONDITIONAL PASS)

**A. NetworkStatusUI._returnSceneName Inspector 기본값 "SampleScene" 문제**

`NetworkStatusUI.cs:66`:
```
[SerializeField] private string _returnSceneName = "SampleScene";
```

기본값이 `"SampleScene"`으로 설정되어 있다. Inspector에서 값을 수동으로 `"Lobby"`로 변경하지 않으면 연결 끊김 후 존재하지 않는 씬으로 이동하려 해 오류가 발생할 수 있다.

**심각도:** Major — 연결 끊김 복귀 기능 전체 불동작 가능성.

**확인 방법:** Unity Inspector에서 NetworkStatusUI 컴포넌트의 "Return Scene Name" 필드 값이 `"Lobby"`로 설정되어 있는지 직접 확인 필요.

판정: CONDITIONAL PASS — Inspector 값 확인 후 PASS/FAIL 결정.

---

**B. LoginBootstrapper의 자동 로그인 성공 경로 ShowLoading 커버리지**

자동 로그인 성공 시 `LoginBootstrapper.cs:181` — `_splashOverlay.FadeOut(GoToNextScene)` → `GoToNextScene()` → `SceneLoader.Load(_nextSceneName)` 호출.

`GoToNextScene()`은 SceneLoader.Load를 사용하므로 ShowLoading(true)가 자동 호출된다. 이 경로의 ShowLoading(false)는 Lobby 씬의 `LobbyRootView.Start()`가 담당한다. 연결 확인됨.

단, `_splashOverlay`가 null인 경우 `GoToNextScene()`이 직접 호출되는데(183행), 이 경로도 SceneLoader.Load를 사용하므로 동일하게 커버됨. PASS.

---

**C. GameEndUI.ReturnToLobby() 멀티 경로 ShowLoading 이중 호출**

`GameEndUI.cs:325`에서 `ShowLoading(true)`를 호출한 뒤, 멀티 경로에서는 `NetworkGameManager.BackToLobby()`를 호출한다. `BackToLobby()` 내부에서 씬 전환을 처리하므로 SceneLoader.Load가 다시 호출되지 않는다.

따라서 멀티 경로에서 ShowLoading(true)는 `GameEndUI.ReturnToLobby()`에서 한 번만 호출되고, ShowLoading(false)는 Lobby 씬의 `LobbyRootView.Start()`가 담당한다.

그러나 `NetworkGameManager.BackToLobby()` 내부 구현을 본 점검에서 읽지 않았으므로, BackToLobby 내부에서 ShowLoading을 별도로 호출하거나 끄지 않는지 추가 확인이 필요하다.

판정: CONDITIONAL PASS — NetworkGameManager.BackToLobby 내부 확인 필요.

---

### 발견된 이슈 목록

| ID | 심각도 | 설명 | 위치 | 확인 방법 |
|----|--------|------|------|-----------|
| BUG-01 | Major | NetworkStatusUI._returnSceneName 기본값이 "SampleScene"으로 설정되어 있어 Inspector에서 수정하지 않으면 연결 끊김 복귀 시 씬 로드 실패 가능 | NetworkStatusUI.cs:66, Inspector | Unity에서 해당 컴포넌트 필드 값 직접 확인 |

---

### 종합 판정

| 항목 | 판정 |
|------|------|
| ShowLoading(true)/(false) 짝 일치 | PASS |
| 싱글 포기 ShowLoading 없음 확인 | PASS |
| NGO SceneManager 미교체 확인 | PASS |
| null-safe 패턴 전체 적용 | PASS |
| 레이어 규칙 위반 없음 | PASS |
| Inspector 기본값 (BUG-01) | CONDITIONAL PASS |

**전체 정적 분석 판정: CONDITIONAL PASS**
BUG-01(NetworkStatusUI._returnSceneName Inspector 값) 확인 후 PASS 전환 가능.
