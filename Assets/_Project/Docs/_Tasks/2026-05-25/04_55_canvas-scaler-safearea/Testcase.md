# Testcase — Canvas Scaler 통일 및 Safe Area 적용

## 테스트 대상

1. Canvas Scaler 설정이 모든 씬에 올바르게 적용됐는지
2. 다양한 해상도에서 UI가 정상적으로 표시되는지
3. Safe Area 영역 안에 UI가 올바르게 배치됐는지
4. 기존 UI 동작(팝업 열기/닫기, 입력 처리 등)이 정상인지

---

## TC 목록

---

### SINGLE-1: Canvas Scaler 설정 확인 — Game 씬

**전제:** 에디터 스크립트 실행 완료. Unity 에디터가 열려 있다.

**동작:**
1. Game.unity 씬을 연다
2. Hierarchy에서 Canvas 오브젝트를 선택한다
3. Inspector의 Canvas Scaler 컴포넌트를 확인한다

**기댓값:**
- 기준 해상도가 가로 1080, 세로 1920으로 표시된다
- 가로/세로 비율 조정 슬라이더가 0(가로 기준)으로 설정되어 있다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### SINGLE-2: Canvas Scaler 설정 확인 — Lobby 씬 두 번째 Canvas

**전제:** 에디터 스크립트 실행 완료. Unity 에디터가 열려 있다.

**동작:**
1. Lobby.unity 씬을 연다
2. Hierarchy에서 두 번째 Canvas 오브젝트를 선택한다
3. Inspector의 Canvas Scaler 컴포넌트를 확인한다

**기댓값:**
- 기준 해상도가 가로 1080, 세로 1920으로 표시된다
- 가로/세로 비율 조정 슬라이더가 0(가로 기준)으로 설정되어 있다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### SINGLE-3: SafeAreaContainer 구조 확인 — Game 씬

**전제:** 에디터 스크립트 실행 완료. Game.unity 씬이 열려 있다.

**동작:**
1. Hierarchy에서 Canvas 오브젝트를 펼친다
2. Canvas 직속 자식 목록을 확인한다

**기댓값:**
- Canvas 아래에 Background와 SafeAreaContainer 두 오브젝트가 있다
- Background는 Canvas 직속에 유지된다
- SafeAreaContainer 아래에 생산 패널, 건물 배치, 건물 액션 패널, 인게임 설정, 확인 팝업, 게임 종료 패널, HUD 오브젝트들이 모두 위치한다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### SINGLE-4: Inspector 연결 확인 — GameBootstrapper

**전제:** 에디터 스크립트 실행 완료. Game.unity 씬이 열려 있다.

**동작:**
1. Hierarchy에서 GameBootstrapper 오브젝트를 선택한다
2. Inspector에서 UI 패널 슬롯들을 확인한다 (생산 패널, 건물 배치, 건물 액션 패널, 인게임 설정, 확인 팝업, 게임 종료 패널, HUD)

**기댓값:**
- 모든 UI 패널 슬롯에 오브젝트가 연결되어 있고 Missing 상태인 항목이 없다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### SINGLE-5: 기본 해상도(1080×1920)에서 인게임 UI 표시

**전제:** Game View 해상도가 1080×1920으로 설정되어 있다.

**동작:**
1. 싱글플레이로 게임을 시작한다
2. 화면을 둘러보며 HUD(골드, 인구, 타이머)를 확인한다
3. 건물 타일을 탭해 건물 배치 패널을 열고 닫는다
4. 배럭을 탭해 생산 패널을 열고 닫는다

**기댓값:**
- HUD가 화면 의도한 위치에 올바르게 표시된다
- 건물 배치 패널이 정상적으로 열리고 배경 탭으로 닫힌다
- 생산 패널이 정상적으로 열리고 배경 탭으로 닫힌다
- UI 요소가 잘리거나 위치가 어긋난 항목이 없다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### SINGLE-6: 긴 화면 해상도(1080×2340)에서 인게임 UI 표시

**전제:** Game View 해상도가 1080×2340(19.5:9)으로 설정되어 있다.

**동작:**
1. 싱글플레이로 게임을 시작한다
2. HUD, 건물 배치 패널, 생산 패널을 차례로 확인한다
3. 인게임 설정 메뉴를 열고 닫는다

**기댓값:**
- 모든 UI가 1080×1920과 동일한 비율로 표시된다
- 위아래 여백이 생기더라도 UI 요소가 화면 밖으로 잘리지 않는다
- 팝업들이 정상적으로 열리고 닫힌다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### SINGLE-7: Safe Area 적용 확인 — 노치/홈바 기기 시뮬레이션

**전제:** Unity Device Simulator가 설치되어 있다. 노치 또는 홈바가 있는 기기 프리셋(예: iPhone 14 Pro)이 선택되어 있다.

**동작:**
1. Device Simulator에서 노치 기기 프리셋을 선택한다
2. 싱글플레이로 게임을 시작한다
3. HUD, 팝업, 버튼 등 모든 UI 요소의 위치를 확인한다

**기댓값:**
- HUD, 버튼, 팝업 등 모든 UI 요소가 노치 및 홈바 영역을 침범하지 않는다
- UI 요소가 Safe Area 안쪽에 올바르게 표시된다
- 배경은 Safe Area 밖으로 자연스럽게 확장된다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### SINGLE-8: 게임 전체 흐름 회귀 테스트

**전제:** 1080×1920 해상도. 싱글플레이 모드.

**동작:**
1. 게임을 시작한다
2. 건물을 배치한다
3. 유닛을 생산한다
4. 설정 메뉴를 열어 포기 버튼을 탭하고 확인 팝업에서 취소를 선택한다
5. 설정 메뉴를 닫는다
6. 게임이 종료될 때까지 진행하거나 포기한다
7. 게임 종료 화면이 표시되는지 확인한다

**기댓값:**
- 각 단계에서 UI가 정상적으로 동작한다
- 팝업 중첩(설정 위에 확인 팝업)이 올바르게 표시되고 닫힌다
- 게임 종료 화면이 올바른 위치에 표시된다
- 전체 흐름에서 UI 오작동, 클릭 불가, 화면 멈춤 현상이 없다

**결과:** 새 task(전체 UI 규칙 검증)로 이관

---

### MULTI-9: 멀티플레이에서 UI 정상 동작 확인

**전제:** 에디터(Host) + 빌드(Client) 구성. 각각 1080×1920 해상도.

**동작:**
1. 호스트와 클라이언트 모두 게임을 시작한다
2. 양쪽에서 HUD, 생산 패널, 건물 배치 패널을 차례로 확인한다
3. 양쪽에서 인게임 설정 메뉴를 열고 닫는다

**기댓값:**
- 호스트와 클라이언트 양쪽 모두 UI가 정상적으로 표시된다
- 각 플레이어의 UI 조작이 상대방 화면에 영향을 주지 않는다

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

## QA 섹션

> 이 섹션은 qa-tester 에이전트 전용 공간입니다.

### 정적 분석 체크리스트

- `SafeAreaFitter.cs`: Awake()에서 Screen.safeArea 읽기 + anchorMin/anchorMax 설정 + offsetMin/offsetMax 초기화 흐름 확인
- `SetupCanvasScaler.cs`: SerializedProperty 경로가 실제 CanvasScaler 필드명과 일치하는지 확인 (`m_ReferenceResolution`, `m_MatchWidthOrHeight`)
- `SetupSafeAreaContainer.cs`: Background 오브젝트가 화이트리스트에서 제외되어 Container 밖에 남는지 확인. SafeAreaContainer RectTransform이 stretch anchor(0,0)~(1,1)으로 설정되는지 확인
- SafeAreaFitter가 ToastUI Canvas에 직접 부착되는지 확인 (ToastUI는 DontDestroyOnLoad이므로 SafeAreaContainer 구조 불필요)
