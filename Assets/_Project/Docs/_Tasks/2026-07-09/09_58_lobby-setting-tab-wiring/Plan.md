# Plan — 로비 설정 탭 배선 완성

## 이 계획이 무엇이고 왜 하는가 (자연어 설명)

로비 화면 하단 탭바에 "설정" 탭 버튼(`SettingTabBtn`)과 설정 화면(`SettingPanel`)은 이미 씬에 만들어져 있지만, 이 둘을 코드가 인식하도록 연결하는 배선이 빠져 있어 설정 탭이 눌러지지도 않고 화면도 나타나지 않습니다. 이 계획은 그 빠진 배선을 세 곳(상태값 정의 / 탭 버튼 View / 패널 전환 View)에 채워, 설정 탭이 나머지 네 탭(전투·상점·프로필·랭킹)과 **완전히 똑같은 방식**으로 동작하도록 만드는 작업입니다.

새 기능을 더하는 것이 아니라, 이전 작업에서 빠뜨린 연결만 정확히 채우는 **버그 수정**입니다. 기존 네 탭의 동작 방식은 전혀 바꾸지 않고 그대로 두며, 설정 탭도 그 방식에 그대로 얹습니다.

> 이번 요청 범위는 이 Plan.md 작성까지입니다. 실제 코드 구현은 사용자가 이 계획을 승인한 뒤 별도로 진행합니다.
> 코드 구현은 **game-programmer 에이전트**에게 위임하는 것이 원칙입니다(CLAUDE.md 규칙 3).

---

## 기존 로직 제거 여부 (WORKFLOW [4] 기존 로직 제거 규칙)

**이 작업에는 기존 로직 제거가 없다.** 세 파일 모두 기존 코드는 그대로 두고 설정 탭 관련 요소를 **추가**하기만 한다(enum 값 추가, 필드 추가, 바인딩/구독 라인 추가). 따라서 비활성화(주석 처리) 대상도, 최종 삭제 대상도 없다.

---

## 근거가 되는 GameSystemRules

Plan 작성 전 `Assets/_Project/Docs/GameSystemRules.md`(인덱스)와 `GameSystemRules/GameSystemRules_UI.md`를 읽었다.

- **공통 UI 규칙 5 (CanvasGroup 숨김/표시 패턴)** — `GameSystemRules_UI.md`
  > UI 요소를 숨길 때 `SetActive(false)` 대신 CanvasGroup을 사용한다. 표시: alpha=1 / blocksRaycasts=true / interactable=true, 숨김: alpha=0 / false / false.

  `LobbyRootView.SetPanelVisible()`이 이미 이 규칙을 정확히 구현하고 있다. 이번에 추가하는 `SettingPanel` 전환도 **같은 `SetPanelVisible` 메서드를 재사용**하므로 규칙 5를 그대로 준수한다. `SettingPanel`의 CanvasGroup 초기값(alpha=0)도 규칙 5의 "숨김" 상태와 일치함을 확인했다.

- **로비 설정/프로필 UI 규칙 1 (ProfilePanel / SettingPanel 최상위 탭 분리)** — `GameSystemRules_UI.md`
  > ProfilePanel과 SettingPanel은 완전히 분리된 별도의 최상위 탭으로 구성한다.

  이번 배선은 바로 이 규칙 1이 요구하는 "SettingPanel을 독립 탭으로" 실제 동작시키기 위한 마무리 작업이다. 규칙 1을 새로 만드는 것이 아니라 이미 문서화된 규칙을 코드로 완성한다.

**GameSystemRules 문서 자체 수정 여부**: 이번 작업은 위 두 기존 규칙을 **준수**하는 것이며 새로운 UI 규칙을 도입하지 않는다. 따라서 GameSystemRules 문서 수정은 이번 범위에 포함하지 않는다. (규칙 1은 이미 SettingPanel 독립 탭을 명시하고 있으므로 보완도 불필요.)

---

## 파일별 변경 내용

### 1. `LobbyViewModel.cs`
경로: `Assets/_Project/Scripts/Presentation/UI/ViewModels/LobbyViewModel.cs`

- `LobbyTab` enum에 `Setting` 값을 추가한다.
  - 변경 전: `public enum LobbyTab { Battle, Shop, Profile, Ranking }`
  - 변경 후(안): `public enum LobbyTab { Battle, Shop, Profile, Setting, Ranking }`
  - **열거 순서 제안**: 씬 탭바의 실제 버튼 배치 순서(Battle → Shop → Profile → **Setting** → Ranking)와 맞추기 위해 `Profile`과 `Ranking` 사이에 삽입한다. enum 값의 순서는 로직에 영향을 주지 않으나(값 비교는 이름 기준), 씬 배치와 읽는 순서를 일치시켜 유지보수 혼동을 줄이기 위함이다.
- 클래스 상단 주석(역할 설명의 `Battle, Shop, Profile, Ranking` 나열)에 `Setting`을 함께 반영한다.
- 근거: 설정 탭을 표현할 상태값이 있어야 `CurrentTab`/`SelectTab`이 설정 탭을 다룰 수 있다.

### 2. `TabBarView.cs`
경로: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/TabBarView.cs`

- `[SerializeField] private Button _settingTabButton;` 필드를 "탭 버튼" 헤더 그룹에 추가한다(기존 4개 버튼 필드와 동일한 형식).
- `Bind(vm)`에 설정 버튼 바인딩 한 줄 추가:
  - `BindButton(_settingTabButton, vm, LobbyViewModel.LobbyTab.Setting);`
- `CurrentTab` 구독의 색상 갱신 블록에 설정 버튼 갱신 한 줄 추가:
  - `UpdateButtonVisual(_settingTabButton, tab == LobbyViewModel.LobbyTab.Setting);`
- `BindButton`/`UpdateButtonVisual` 헬퍼는 이미 `null` 안전하므로(`if (button == null) return;`) 씬 배선 전에도 컴파일/런타임 오류 없이 동작한다.
- 근거: 클릭 리스너 연결(→ 클릭 시 설정 탭 전환)과 색상 갱신 대상 포함(→ "항상 선택된 것처럼" 보이던 증상 해소). 나머지 4개 버튼과 완전히 대칭 구조.

### 3. `LobbyRootView.cs`
경로: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs`

- "탭 패널" 헤더 그룹에 `[SerializeField] private GameObject _settingPanel;` 필드를 추가한다(툴팁: "설정 탭 패널 (LobbySettingsView 부착 오브젝트)").
- 내부 CanvasGroup 캐시 필드 추가: `private CanvasGroup _settingPanelGroup;`
- `Awake()`에 캐시 한 줄 추가: `_settingPanelGroup = _settingPanel?.GetComponent<CanvasGroup>();`
- `Start()`의 `CurrentTab` 구독 블록에 표시 전환 한 줄 추가:
  - `SetPanelVisible(_settingPanelGroup, tab == LobbyViewModel.LobbyTab.Setting);`
- 상단 주석의 씬 계층 예시(ContentArea 하위 패널 나열)에 `SettingPanel`을 추가 반영한다.
- `SetPanelVisible`는 기존 메서드를 그대로 재사용한다(공통 UI 규칙 5). 신규 헬퍼 불필요.
- 근거: 설정 탭 선택 시 `SettingPanel`을 표시(alpha=1)하고 다른 탭 선택 시 숨김(alpha=0)으로 전환. 현재는 참조가 없어 alpha=0인 채로 방치되던 것을 해소.

---

## 씬(Inspector) 배선 작업

코드 필드만 추가하면 씬의 `TabBarView`/`LobbyRootView` 컴포넌트에 새 슬롯이 생기지만 **값은 비어 있다(None)**. 아래 두 참조를 실제로 연결해야 동작한다.

| 연결 대상 컴포넌트 | 새 슬롯 | 연결할 오브젝트 | 확인된 fileID |
|--------------------|---------|-----------------|---------------|
| TabBar 의 `TabBarView` | `_settingTabButton` | `SettingTabBtn`의 Button 컴포넌트 | 1351999588 |
| LobbyRoot 의 `LobbyRootView` | `_settingPanel` | `SettingPanel` GameObject | 1670590791 |

### 배선 방식 — 제안 (사용자 확인 필요)

WORKFLOW [5-2]에 따라 Inspector 수동 작업은 1회성 Editor 스크립트로 자동화할 수 있다. 두 가지 방식을 제안하며, **어느 방식으로 진행할지는 사용자가 결정한다**(현재 미정).

- **방식 A — 사용자 수동 Inspector 배선 (제안 기본값)**
  - 이번 배선은 참조 2개뿐이고(Button 1개 + GameObject 1개), 연결 대상 오브젝트 이름(`SettingTabBtn`, `SettingPanel`)과 위치가 명확하다.
  - 직전 사운드 작업(`_Tasks/2026-07-07/12_28_sound-system-bugfix/` 계열)에서 Editor 스크립트 자동 배선 시 여러 참조를 한꺼번에 다루다 두 군데에서 잘못 연결되는 버그가 났던 전례가 있다. 참조 수가 적은 이번 경우엔 수동 배선이 오히려 안전하고 빠를 수 있다.
  - 절차: Unity 에디터에서 `Lobby.unity`를 열고 TabBar의 `TabBarView` 인스펙터에서 `_settingTabButton`에 `SettingTabBtn`을, LobbyRoot의 `LobbyRootView` 인스펙터에서 `_settingPanel`에 `SettingPanel`을 드래그하여 연결 후 씬 저장.

- **방식 B — 1회성 Editor 스크립트 자동 배선**
  - `Hexiege/...` 메뉴 항목으로 실행하여 두 참조를 코드로 찾아 할당하고 `EditorUtility.SetDirty()` + 씬 저장까지 수행하는 1회성 스크립트를 작성.
  - 자동화 시 반드시 지킬 것(공용 MEMORY 교훈): 참조 할당 후 `EditorUtility.SetDirty()` 및 씬 저장(`EditorSceneManager.MarkSceneDirty`/`SaveScene`) 호출이 없으면 변경이 저장되지 않는다.
  - 실행 완료 확인 후 스크립트 파일은 삭제 가능(1회성).

> **미정 — 배선 방식 결정**: 방식 A(수동)와 B(자동) 중 무엇으로 진행할지 사용자 확인 필요. 참조가 단순(2개)하다는 점에서 방식 A를 기본 제안하나, 사용자가 자동화를 원하면 방식 B로 스크립트를 작성한다.

---

## 예상 위험 요소 및 아키텍처 제약

- **레이어 경계**: 세 파일 모두 Presentation 레이어이며 레이어 경계를 넘는 변경이 없다. NGO/Netcode 참조 변경 없음.
- **enum 순서 변경 영향**: `Setting`을 `Ranking` 앞에 삽입해도 값 비교는 이름 기준(`tab == LobbyTab.Setting`)이라 기존 로직에 영향 없음. 다만 `LobbyTab`을 정수로 캐스팅해 저장/전송하는 곳이 있으면 순서가 의미를 가질 수 있으므로, 구현 시 game-programmer가 `(int)LobbyTab` 사용처가 없는지 확인한다(현재 확인된 사용처 없음 — 구현 시 재확인 대상).
- **씬 배선 누락 시 동작**: 코드만 반영하고 Inspector 배선을 하지 않으면, 헬퍼의 null 가드 덕분에 오류는 없으나 설정 탭은 여전히 무반응이다 → 반드시 씬 배선까지 완료해야 버그가 해소된다.
- **CanvasGroup 부재 가정**: `SettingPanel`에는 CanvasGroup이 이미 있음을 확인했으므로 `_settingPanelGroup`은 정상 캐시된다. (없었다면 표시 전환이 무시됨 — 현재는 해당 없음.)

---

## 수정 대상 파일 요약

[수정 — 코드]
- `Assets/_Project/Scripts/Presentation/UI/ViewModels/LobbyViewModel.cs` (enum 값 + 주석)
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/TabBarView.cs` (필드 + 바인딩 + 색상 갱신)
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs` (필드 + CanvasGroup 캐시 + 표시 전환 + 주석)

[수정 — 씬]
- `Assets/_Project/Scenes/Lobby.unity` (`TabBarView._settingTabButton`, `LobbyRootView._settingPanel` 참조 연결 — 방식 A 또는 B로 진행)

[임시 — 방식 B 선택 시에만]
- 1회성 Editor 배선 스크립트 (실행 후 삭제 가능)

---

## 미정 / 확인 필요 항목

| 항목 | 상태 |
|------|------|
| 씬 배선 방식 (수동 방식 A vs 자동 Editor 스크립트 방식 B) | **미정** — 사용자 결정 필요 (방식 A 기본 제안) |
| `(int)LobbyTab` 캐스팅 사용처 존재 여부 | 현재 확인된 사용처 없음 — 구현 시 game-programmer가 재확인 |
| GameSystemRules 문서 수정 | 불필요로 판단(기존 규칙 5 및 로비 규칙 1 준수 작업) |

---

## 완료 결과 (2026-07-10, 사용자 실기 PASS)

계획대로 3파일 배선을 **추가**하여 설정 탭이 나머지 네 탭과 동일하게 동작하도록 완성했다. 기존 로직 제거는 없었다.

- **`LobbyViewModel.cs`**: `LobbyTab` enum에 `Setting` 값을 계획대로 `Profile`과 `Ranking` 사이에 추가 → `{ Battle, Shop, Profile, Setting, Ranking }`. (씬 탭바 배치 순서와 일치)
- **`TabBarView.cs`**: `_settingTabButton` 필드 추가, `Bind()`에 `BindButton(_settingTabButton, vm, LobbyTab.Setting)` + 색상 갱신 `UpdateButtonVisual(_settingTabButton, tab == LobbyTab.Setting)` 추가. → 클릭 무반응 및 "항상 선택된 것처럼" 보이던 증상 해소.
- **`LobbyRootView.cs`**: `_settingPanel` 필드 + `_settingPanelGroup` CanvasGroup 캐시 + `SetPanelVisible(_settingPanelGroup, tab == LobbyTab.Setting)` 전환 추가. → 설정 화면이 표시되지 않던 문제 해소.
- **씬 배선**: 계획에서 제안한 방식(수동 vs Editor 스크립트) 중, 이 작업의 참조 2개는 이전 task(`06_09`)에서 도입한 `SetupVolumeProfileUI_20260709.cs` Editor 배선 흐름과 함께 `Lobby.unity`에 반영되었다. (이전 task에서 이름 기반 자동 매칭이 `_backButton`을 `OffButton`에 오연결한 사례가 있었던 만큼, 참조가 적은 배선은 수동 확인을 병행하는 것이 안전하다는 교훈을 재확인.)

**GameSystemRules 문서 수정 없음** — 계획대로 기존 공통 규칙 5(CanvasGroup) 및 로비 규칙 1(ProfilePanel/SettingPanel 분리)을 준수하는 배선 작업으로, 신규 규칙 불필요.

**실기 확인(사용자, 2026-07-10 PASS)**: 로비 하단 설정 탭 버튼 클릭 시 설정 화면 정상 전환, 탭 버튼 색상 강조 정상(선택된 탭만 밝게), 나머지 네 탭 동작 무영향.

> Testcase.md는 작성하지 않음 — 사용자가 TC/QA를 명시적으로 요청하지 않았으며, 실기 결과는 본 섹션으로 대체 기록한다.
