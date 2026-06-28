# Testcase — 전역 UI 시스템 도입 (UIManager + SplashOverlay)

이 문서는 Login 씬에서 1회 생성되어 모든 씬에서 공유되는 전역 UI 시스템
(UIManager + SplashOverlayView)이 기획대로 동작하는지 확인하기 위한 테스트 시나리오다.
앱 진입부터 로그인, 로비, 게임 씬까지의 전체 흐름에서 스플래시 화면 표시,
공통 확인 팝업, 로딩 표시가 정상 동작하는지 사용자 실기로 검증한다.

---

### TC-01: 앱 진입 시 SplashOverlay "로딩 중..." 표시

**전제:** 앱을 종료한 상태. Login 씬이 시작 씬으로 설정되어 있다.

**동작:**
1. 앱을 실행한다.

**기댓값:**
- 화면 전체에 스플래시 배경 이미지가 표시된다.
- 화면 중앙(또는 안내 영역)에 "로딩 중..." 문구가 표시된다.
- 아직 로그인 화면은 보이지 않는다.

**결과:** PASS

---

### TC-02: 초기화 완료 후 "Tap to Start" 깜빡임

**전제:** TC-01 상태에서 사운드/Firebase/UIManager 등 초기화가 진행 중이다.

**동작:**
1. 초기화가 완료될 때까지 기다린다.

**기댓값:**
- "로딩 중..." 문구가 사라진다.
- "Tap to Start" 문구가 나타나고, 서서히 밝아졌다 어두워지기를 반복하며 깜빡인다.
- 배경 이미지는 그대로 유지된다.

**결과:** PASS

---

### TC-03: 탭 시 SplashOverlay 페이드아웃 → 로그인 화면 표시

**전제:** TC-02 상태로 "Tap to Start"가 깜빡이고 있다. 자동 로그인 대상 계정이 없다.

**동작:**
1. 화면을 한 번 터치한다.

**기댓값:**
- 스플래시 화면 전체가 서서히 투명해지며 사라진다(페이드아웃).
- 페이드아웃이 끝나면 로그인 화면이 표시된다.

**결과:** PASS

---

### TC-04: 자동 로그인 성공 시 SplashOverlay 페이드아웃 → Lobby 이동

**전제:** 이전에 로그인하여 세션이 보존되어 있다. 앱을 다시 실행한 상태.

**동작:**
1. 앱을 실행하고 초기화 및 자동 로그인이 완료될 때까지 기다린다.

**기댓값:**
- 자동 로그인이 성공한다.
- 별도의 탭 조작 없이 스플래시 화면이 페이드아웃된다.
- 로그인 화면을 거치지 않고 로비 화면으로 이동한다.

**결과:** PASS

---

### TC-05: Lobby 씬에서 UIManager.ShowConfirm 동작

**전제:** 로비 화면에 진입한 상태. (예: 프로필 화면에서 로그아웃 등 확인이 필요한 조작)

**동작:**
1. 확인 팝업이 필요한 동작(예: 로그아웃)을 수행한다.

**기댓값:**
- 화면 최상단에 공통 확인 팝업이 표시된다.
- 메시지와 확인/취소 버튼이 정상 표시된다.
- 확인을 누르면 해당 동작이 실행되고, 취소를 누르면 팝업이 닫힌다.
- 로비 씬에 별도의 ConfirmPopup이 없어도 전역 UIManager의 팝업이 정상 동작한다.

**결과:** PASS

---

### TC-06: Game 씬에서 UIManager.ShowLoading 동작 (포기 → 확인 → 로딩 표시)

**전제:** 게임 씬에서 플레이 중이며, 인게임 설정 메뉴를 열 수 있는 상태.

**동작:**
1. 인게임 설정 메뉴에서 게임 포기를 선택한다.
2. 확인 팝업에서 확인을 누른다.

**기댓값:**
- 포기 확인 팝업이 전역 UIManager를 통해 정상 표시된다.
- 확인을 누르면 로딩 표시(스피너 + 안내 문구)가 화면에 나타난다.
- 게임 씬에 별도의 LoadingScreen이 없어도 전역 UIManager의 로딩 표시가 정상 동작한다.

**결과:** PASS

---

### TC-07: Login 씬 직접 진입 시 null-safe 무시 (에러 없음)

**전제:** 에디터에서 Lobby.unity 또는 Game.unity를 UIManager가 생성되지 않은 상태로 직접 실행한다.

**동작:**
1. UIManager가 없는 씬을 단독으로 실행한다.
2. ShowConfirm / ShowLoading / ToastUI 호출이 발생하는 동작을 수행한다.

**기댓값:**
- UIManager.Instance가 null이어도 NullReferenceException이 발생하지 않는다.
- null-safe 호출(`UIManager.Instance?....`)로 해당 UI 표시만 생략되고 게임 진행은 정상이다.

**결과:** PASS

---

## QA 섹션

본 작업은 사용자 실기 테스트로 검증되었다.

- **판정**: TC-01 ~ TC-07 전체 **PASS** (사용자 실기, 2026-06-18)
- **설계 근거**:
  - UIManager는 `SingletonMonoBehaviour<UIManager>` + `IUIManager`로 Login 씬에서 1회 생성 후 DontDestroyOnLoad로 전 씬 공유.
  - ConfirmPopup / LoadingIndicator는 UIManager Canvas(SortingOrder 100) 하위에 임베드.
  - SplashOverlay는 전용 Canvas(SortingOrder 200), Background(SafeArea 밖) + SafeAreaContainer(텍스트 안) 구조.
  - `ShowLoading(bool show, string message = "")`로 모든 로딩 사유(씬 전환/Firebase/매칭 등)를 단일 API로 통합.
- **null-safe**: UIManager 미생성 씬 단독 실행 시 `UIManager.Instance?.` 패턴으로 안전 무시(TC-07).
