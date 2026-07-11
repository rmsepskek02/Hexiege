# Research — 로비 설정 탭 배선 누락 버그

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

로비 화면 하단에는 전투/상점/프로필/랭킹 탭을 전환하는 탭바가 있습니다. 탭을 누르면 그 탭의 화면(패널)만 보이고, 버튼 색상도 눌린 탭만 밝게 강조됩니다.

최근 다른 작업에서 "프로필" 화면 안에 섞여 있던 **설정(사운드 등)** 기능이 "설정 탭"이라는 별도의 독립 탭으로 분리되어 나왔습니다. 그런데 이 새 설정 탭을 **탭바가 인식하도록 연결하는 배선 작업이 누락**되어, 실기 테스트에서 다음 두 가지 증상이 발견되었습니다.

1. 설정 탭 버튼을 눌러도 아무 반응이 없다 (화면이 전환되지 않음).
2. 설정 탭 버튼이 항상 "선택된 것처럼" 밝게 표시된다 (실제로는 선택되지 않았는데도).

이번 작업의 목적은 이 배선을 완성하여, 설정 탭이 나머지 네 탭(전투/상점/프로필/랭킹)과 **완전히 동일한 방식으로** 정상 작동하도록 만드는 것입니다. 새로운 기능을 추가하는 것이 아니라, 이전 작업에서 빠뜨린 연결을 채워 넣는 버그 수정 작업입니다.

> 참고: 이번 요청 범위는 Research.md와 Plan.md 작성까지입니다. 실제 코드 구현은 사용자가 Plan.md를 승인한 뒤 별도로 진행합니다.

---

## 선행 작업

- 이전 작업 `_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/` 에서 로비 UI를 `ProfilePanel`과 `SettingPanel`로 완전히 분리했다(각각 별도 최상위 GameObject). 이 작업은 볼륨/뮤트 로직 구현에 집중했고, **로비 하단 탭바가 "설정" 탭을 인식하도록 배선하는 작업은 누락**되었다.
- 이번 실기 테스트에서 사용자가 위 증상을 발견하여 별도 task로 문서화하게 되었다.

---

## 현재 상태 (코드/씬 직접 확인 — 추정 아님)

### 1. `LobbyViewModel.cs`
경로: `Assets/_Project/Scripts/Presentation/UI/ViewModels/LobbyViewModel.cs`

- `LobbyTab` enum이 4개 값만 가진다 — `Setting` 값이 아예 없다.
  ```csharp
  public enum LobbyTab { Battle, Shop, Profile, Ranking }   // Setting 없음
  ```
- `CurrentTab`(ReactiveProperty, 기본값 Battle)와 `SelectTab`(Subject)이 있으며, 생성자에서 `SelectTab` → `CurrentTab` 갱신을 구독한다. 탭 값 자체가 없으므로 설정 탭을 표현할 방법이 없다.

### 2. `TabBarView.cs`
경로: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/TabBarView.cs`

- 버튼 필드가 4개만 있다 — `_settingTabButton` 필드 자체가 없다.
  ```csharp
  [SerializeField] private Button _battleTabButton;
  [SerializeField] private Button _shopTabButton;
  [SerializeField] private Button _profileTabButton;
  [SerializeField] private Button _rankingTabButton;
  ```
- `Bind(vm)`에서 위 4개 버튼만 `BindButton`으로 커맨드에 연결하고, `CurrentTab` 구독 시 4개 버튼만 `UpdateButtonVisual`로 색상을 갱신한다.
- 설정 탭 버튼은 참조 자체가 없어 ① 클릭 리스너가 걸리지 않고(→ 클릭 무반응), ② 색상 갱신 대상에서도 빠진다(→ 씬/프리팹에 저장된 정적 색상이 그대로 남아 "항상 선택된 것처럼" 보임).

### 3. `LobbyRootView.cs`
경로: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs`

- 패널 GameObject 필드가 4개만 있다 — `_settingPanel` 필드 자체가 없다.
  ```csharp
  [SerializeField] private GameObject _battlePanel;
  [SerializeField] private GameObject _shopPanel;
  [SerializeField] private GameObject _profilePanel;
  [SerializeField] private GameObject _rankingPanel;
  ```
- `Awake()`에서 4개 패널의 `CanvasGroup`을 캐시하고, `Start()`의 `CurrentTab` 구독에서 `SetPanelVisible`로 4개 패널만 표시/숨김한다.
- `SetPanelVisible`는 이미 **공통 UI 규칙 5(CanvasGroup 숨김/표시 패턴)** 를 그대로 따른다(alpha/blocksRaycasts/interactable 전환, null 안전).
- `SettingPanel`은 참조가 없어 탭 전환 로직에서 완전히 제외되어 있다.

### 4. 씬 구조 (`Assets/_Project/Scenes/Lobby.unity`, 직접 확인)

- `TabBar`(TabBarView 부착) 하위에 **버튼 5개**가 존재하며 배치 순서는 다음과 같다(HorizontalLayoutGroup 사용):

  | 순서 | 오브젝트 이름 | Button 컴포넌트 fileID | TabBarView 배선 여부 |
  |------|--------------|------------------------|---------------------|
  | 1 | BattleTabBtn | 292581208 | O (`_battleTabButton`) |
  | 2 | ShopTabBtn | 458227965 | O (`_shopTabButton`) |
  | 3 | ProfileTabBtn | 1470958461 | O (`_profileTabButton`) |
  | 4 | **SettingTabBtn** | **1351999588** | **X (필드 없음)** |
  | 5 | RankingTabBtn | 607533777 | O (`_rankingTabButton`) |

  > 주의: 설정 탭 버튼의 이름은 `SettingTabBtn`이며, **탭바 상 위치는 랭킹 탭보다 앞(4번째)** 이다. 이전 작업 시점 예상과 달리 마지막이 아니다.

- `TabBarView` 컴포넌트의 실제 직렬화 값에도 `_battleTabButton`/`_shopTabButton`/`_profileTabButton`/`_rankingTabButton` 4개만 존재하고 설정 버튼 슬롯이 없음을 확인했다.

- `SettingPanel`(GameObject fileID `1670590791`, `ContentArea` 하위)에는 **자체 CanvasGroup이 이미 부착**되어 있고, 그 초기값은 다음과 같다(직접 확인 — 미정 아님):

  | 속성 | 값 |
  |------|-----|
  | Alpha | **0** |
  | Interactable | 0(false) |
  | BlocksRaycasts | 0(false) |

  즉 `SettingPanel`은 **숨김 상태(alpha=0)로 시작**한다. 그런데 `LobbyRootView`가 이 패널을 참조하지 않으므로 어떤 탭을 눌러도 다시 표시(alpha=1)되지 않는다 → **설정 탭은 눌러도 화면이 절대 나타나지 않는다.** (다른 탭으로 이동 시 계속 표시되어 겹치는 문제는 없음 — 초기값이 이미 숨김이기 때문.)

- `SettingPanel` 루트에는 `LobbySettingsView` 스크립트가 부착되어 있으며 사운드 슬라이더/버튼 참조들은 이미 배선되어 있다(설정 탭 내부 기능 자체는 구성 완료 상태, 탭 진입 배선만 누락).

---

## 근본 원인 요약

설정 탭은 씬 상에는 버튼(`SettingTabBtn`)과 패널(`SettingPanel`, CanvasGroup 포함)이 모두 준비되어 있으나, 이를 코드가 인식하는 **3단 배선**이 전부 빠져 있다.

1. `LobbyViewModel.LobbyTab` enum에 `Setting` 값 없음 → 설정 탭을 표현할 상태값 부재.
2. `TabBarView`에 설정 버튼 필드/바인딩/색상 갱신 없음 → 클릭 무반응 + 항상 선택된 것처럼 보임.
3. `LobbyRootView`에 설정 패널 필드/CanvasGroup 캐시/표시 전환 없음 → 설정 화면이 절대 표시되지 않음.

세 곳 모두 채워야 설정 탭이 나머지 탭과 동일하게 동작한다.

---

## 영향 범위

- 수정 대상 코드는 로비 탭 전환에 한정된다(`LobbyViewModel`, `TabBarView`, `LobbyRootView` 3개 파일).
- `LobbyTab` enum에 값을 **추가**하는 것이므로 기존 4개 값의 순서/의미는 바뀌지 않는다 → 기존 탭 동작에 영향 없음.
- 인게임(Game 씬) 설정 메뉴, 사운드 로직, `LobbySettingsView` 내부 동작은 이번 변경과 무관하다(설정 탭 진입 이후 동작이므로).
- 씬(`Lobby.unity`)의 `TabBarView`/`LobbyRootView` 컴포넌트에 새 참조 슬롯을 연결하는 **Inspector 배선 작업**이 추가로 필요하다(코드 필드 추가만으로는 씬에 값이 채워지지 않음).

---

## 미해결/확인 필요 항목

| 항목 | 상태 |
|------|------|
| 씬의 설정 버튼 오브젝트 이름 | **확인 완료** — `SettingTabBtn` (TabBar 4번째 자식, Button fileID 1351999588) |
| SettingPanel CanvasGroup 초기 alpha | **확인 완료** — alpha=0 (숨김으로 시작) |
| 씬 배선 방식(Editor 자동 스크립트 vs 사용자 수동 Inspector) | **미정** — Plan.md에서 제안 후 사용자 결정 필요 |
| GameSystemRules 문서 수정 필요 여부 | **미정** — Plan.md 작성 중 판단 (현재로선 기존 규칙 준수 작업으로, 신규 규칙 불필요 가능성 높음) |
