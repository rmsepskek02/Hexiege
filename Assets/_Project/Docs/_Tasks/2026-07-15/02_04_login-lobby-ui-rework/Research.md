# Research — 닉네임 설정 화면 + 프로필/랭킹 탭 UI 재작업

## 이 작업이 왜 필요한가 (자연어 설명)

닉네임 설정 화면(Login 씬), 프로필 탭, 랭킹 탭(Lobby 씬)은 모두 기능적으로는 이미 동작한다.
다만 세 화면 모두 "Hexiege/Setup/..." 메뉴의 에디터 1회성 스크립트가 자동으로 찍어낸 UI라서,
버튼은 단색 사각형이고, 세로로 늘어선 항목들이 전부 같은 높이로 뭉툭하게 배치되어 있으며,
스프라이트(패널 배경, 버튼 이미지 등)가 하나도 적용되어 있지 않다. 또한 몇 가지 프로세스가
기획 의도와 다르게 동작한다 — 예를 들어 닉네임 설정 화면은 반드시 통과해야 하는 필수 화면인데
Android 뒤로가기로 빠져나갈 수 있는 구멍이 있고, 프로필/랭킹 탭에는 "새로고침" 버튼이 아예 없다.

이 문서는 세 화면의 **현재 상태를 코드와 씬 파일 근거를 들어 감사**한 결과다. 구현은 하지 않으며,
이 감사 결과를 바탕으로 Plan.md에서 재설계 명세를 작성한다.

## 감사 대상

| 화면 | 씬 | 에디터 생성 스크립트 | View |
|------|-----|----------------------|------|
| 닉네임 설정 화면 | Login.unity | `Assets/Editor/Setup/CreateNicknameSetupPanel.cs` | `NicknameSetupView.cs`, `LoginRootView.cs` |
| Profile 탭 | Lobby.unity | `Assets/Editor/Setup/CreateProfileStatsFields.cs` | `ProfileView.cs` |
| Ranking 탭 | Lobby.unity | `Assets/Editor/Setup/CreateRankingTable.cs` | `RankingView.cs`, `RankRowView.cs` |

감사 기준: `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` 공통 UI 규칙(1, 2, 4, 5, 6, 8~10) +
"닉네임 설정 화면" / "Profile 탭 UI" / "Ranking 탭 UI" / "로비 설정/프로필 UI" 각 섹션.

---

## 1. 닉네임 설정 화면 (Login.unity)

### 1-1. 레이아웃 문제

- `CreateNicknameSetupPanel.cs` 96~99행: 패널 배경이 `Image.color = (0.08, 0.09, 0.12, 0.96)` 단색이다.
  `panelBg.sprite`를 지정하는 코드가 없다 — `ui_panel_dark`/`ui_panel_light` 스프라이트 미사용(규칙 1 반응형과는
  무관하지만 확정 결정 5 "스프라이트 적용" 미충족).
- 110~116행: Content의 `VerticalLayoutGroup`이 `childForceExpandWidth = true`, **`childForceExpandHeight = true`**로
  설정되어 있다. 자식은 Title / NicknameInput / ConfirmButton / SkipButton / StatusText **5개**이며, 이 설정으로는
  5개 요소가 세로 공간을 동일 비율로 나눠 갖는다 — 제목·입력창·버튼·상태문구가 전부 비슷한 큰 높이를 차지해
  뭉툭한 레이아웃이 된다. (`.claude/agent-memory/game-design-lead` 참고 대상 아님 — 이 교훈은
  MEMORY.md에 "ChildForceExpandHeight만으론 부족, LayoutElement 비율 가중치 필요"로 기 기록되어 있음.)
- 128~134행: 확인/스킵 버튼도 `EnsureButton()`으로 생성되며 `img.color`만 설정하고 `img.sprite`는 설정하지 않는다
  → `ui_btn_gold`/`ui_btn_silver` 등 스프라이트 미적용.
- 243~284행 `EnsureInputField()`: 입력창 배경도 `bg.color = Color.white`만 설정 — `ui_input_light`/`ui_input_dark`
  스프라이트 미적용.

### 1-2. 프로세스 문제 — 뒤로가기 탈출 구멍 (확정 결정 1 위반)

- `LoginRootView.cs` 270~279행 `Update()`: `Keyboard.current.escapeKey.wasPressedThisFrame`이면 **패널 종류를
  구분하지 않고** 항상 `HandleBack()`을 호출한다(Android 뒤로가기는 New Input System에서 Escape로 매핑되는
  전제로 작성된 코드).
- `HandleBack()`(219~242행)은 `_backStack.Count > 0`이면 스택에서 pop한 이전 패널로 그냥 전환한다. 패널이
  `NicknameSetup`인지 여부를 전혀 검사하지 않는다.
- `ShowNicknameSetup()`(176~184행)은 호출 시 `PushCurrentToStack()`으로 **직전 화면(LoginSelect 또는
  EmailVerify)을 스택에 push**한다.
- 결론: 닉네임 설정 화면이 떠 있는 상태에서 Android 뒤로가기(에디터에서는 ESC)를 누르면 스택에 쌓인 직전
  화면으로 즉시 복귀한다. 즉 **필수 통과 화면인 닉네임 설정을 건너뛰고 로그인 선택/이메일 인증 화면으로
  돌아갈 수 있다** — 확정 결정 1("뒤로가기 완전 차단")을 현재 코드는 전혀 구현하지 않고 있다.

### 1-3. 프로세스 문제 — 확인 버튼 상시 활성

- `NicknameSetupView.cs` 87행: `_confirmButton.onClick.AddListener(OnConfirmClicked)`만 등록될 뿐, 입력 필드
  `onValueChanged`를 구독해 버튼 `interactable`을 갱신하는 로직이 없다.
- `OnConfirmClicked()`(125~164행)는 클릭된 후에야 `ValidateNickname()`으로 검증하고 실패 시 `_statusText`에
  메시지만 표시한다(139행 `SetInteractable(false)`은 저장 처리 중 잠금 목적이지 빈 값 방지 목적이 아님).
- GameSystemRules_UI.md 닉네임 설정 화면 규칙 2는 "빈 값이면 확인 버튼 클릭 불가 **또는** 안내 메시지 표시"로
  두 방식을 모두 허용하므로 이 자체는 문서 위반은 아니지만, 사용자 확정 요구(과업 지시)는 버튼 상시 활성 상태를
  전제로 하지 않으므로 Plan에서 재검토 대상으로 표시한다.

### 1-4. 규칙 준수 확인 (씬 실측)

- `Login.unity`에서 `NicknameSetupPanel`(GameObject fileID `212277171`)의 RectTransform 부모 체인을 추적한
  결과: `NicknameSetupPanel` → 부모 `LoginRoot`(fileID `395511956`/`395511957`) → 부모
  `SafeAreaContainer`(fileID `1438250020`/`1438250021`, 1157~11190행). **NicknameSetupPanel은
  SafeAreaContainer 하위에 정상 배치되어 있다** (규칙 4 준수, 추정 아님 — 씬 파일 fileID 추적으로 확인).
- 폰트: `CreateNicknameSetupPanel.cs` 39~40행에서 Light/Bold SDF 경로를 로드해 명시적으로 지정 — 규칙 6 준수.
- CanvasGroup 표시/숨김: `LoginRootView.ShowGroup()`/`HideGroup()`(335~354행)이 alpha/blocksRaycasts/interactable을
  사용 — 규칙 5 준수.
- 팝업 타입 규칙(8~10)은 닉네임 설정 화면이 팝업이 아닌 전체화면 필수 패널이므로 해당 없음.

---

## 2. Profile 탭 (Lobby.unity)

### 2-1. 레이아웃 문제 — 두 레이아웃 트리 겹침

`Lobby.unity` 씬 파일을 fileID로 추적한 결과, `ProfilePanel`(fileID `576258266`)의 자식은 정확히 2개다
(5424~5426행):

- `LobbyProfileView`(fileID `1030340521`/`1030340522`) — 앵커 `(0,0)~(1,1)` **전체 스트레치**. 자식으로
  `BackButton`, `MainView`(fileID `1828005916`/`1828005917`, 앵커 역시 `(0,0)~(1,1)` 전체 스트레치),
  `SubViewContainer`를 가진다. 이 트리는 계정 정보/로그아웃/연동 버튼 등 **기존에 수작업으로 구성된 UI**다.
- `ProfileStatsContainer`(fileID `1022946599`/`1022946600`) — `CreateProfileStatsFields.cs`가 생성한 것으로,
  앵커가 `(0.08, 0.45)~(0.92, 0.95)`(9920~9921행), 즉 **화면 상단~중단(y 45%~95%) 밴드**를 차지한다.

`ProfilePanel`의 `m_Children` 순서는 `[LobbyProfileView, ProfileStatsContainer]`이므로 ProfileStatsContainer가
나중에 그려진다(위에 겹침). `MainView`가 전체 스트레치이고 그 내부에 계정정보 텍스트 등 기존 UI 요소가 배치돼
있으므로, **두 개의 독립된 레이아웃 트리가 화면 y 45~95% 구간에서 서로 겹칠 수 있는 구조**임이 씬 파일로
확인된다(정확히 어떤 개별 텍스트끼리 겹치는지는 MainView 내부 자식까지 전부 추적해야 하나, 두 트리가 같은
부모 아래 겹친 영역을 차지하는 구조적 사실 자체는 fileID 추적으로 확정됨).

- `CreateProfileStatsFields.cs` 65~71행: `ProfileStatsContainer`의 `VerticalLayoutGroup`도
  `childForceExpandHeight = true` — 닉네임행/총게임/승/패/승률/마지막접속/랭킹 **7개 행이 균등 높이**로
  분배되어 뭉툭하다(닉네임 설정 화면과 동일한 문제 패턴).
- 버튼(`_changeNicknameButton`)도 `EnsureButton()`으로 생성되어 `img.color`만 설정 — 스프라이트 미적용.

### 2-2. 프로세스 문제 — 새로고침 버튼 없음 (확정 결정 2 미구현)

- `ProfileView.cs`에서 `RefreshProfileDataAsync()`를 호출하는 지점은 `Start()`(180행)와 `OnEnable()`(200행)
  뿐이다. "전적" 옆에 새로고침 버튼을 만들고 클릭 시 재호출하는 코드/슬롯이 전혀 없다.
- `RankingView.cs` 주석(20~24행)과 동일한 구조적 이유로, `OnEnable()`은 탭이 CanvasGroup 방식으로 전환되므로
  **씬 진입 시 1회만 발화**하고 탭 클릭마다 재발화하지 않는다(코드 194~202행 주석에 명시). 즉 현재 구조에서는
  로비 진입 이후 전적/랭킹을 갱신할 방법이 전혀 없다.

### 2-3. 프로세스 문제 — 닉네임 변경 미구현 (확정 결정 3 미구현)

- `ProfileView.OnChangeNicknameClicked()`(334~345행): 모달 팝업 없이 `SetStatus()`로 안내 텍스트만 표시한다
  ("최초 1회 닉네임 변경은 무료입니다. (준비 중)" 또는 "닉네임 변경은 인앱 결제로 가능합니다. (준비 중)").
  실제 입력·검증·저장 로직은 없다(332행 주석 `// TODO: 닉네임 변경 UI(입력/검증/결제 분기) 별도 구현 필요`로
  이미 표시돼 있음).
- `PlayerProfileUseCase.SaveNicknameAsync()`(107~121행)는 호출될 때마다 `GenerateCode()`로 **새 4자리 코드를
  항상 새로 생성**한다(117행). 확정 결정 3은 "코드(#4729)는 변경해도 유지"를 요구하므로, 현재 이 메서드를
  그대로 재사용하면 닉네임 변경 시 코드가 의도치 않게 바뀐다 — 신규 로직에서 반드시 별도 처리해야 하는
  충돌 지점이다.
- `IPlayerProfileService.SaveNicknameAsync(nickname, code)`(`IPlayerProfileService.cs` 36행)에는
  `hasUsedFreeNicknameChange` 값을 저장하는 파라미터/오버로드가 없다. `PlayerProfileService.cs`
  55행에 `KeyHasUsedFreeNicknameChange` 상수는 이미 정의돼 있고 `LoadProfileAsync()`(89행)는 이 값을
  읽지만, `SaveNicknameAsync()`(103~125행) 저장 로직에는 이 키를 쓰는 코드가 없다 — 즉 **현재 인터페이스로는
  hasUsedFreeNicknameChange를 true로 저장할 방법 자체가 없다**(읽기만 가능, 쓰기 불가).

### 2-4. 규칙 준수 확인 (씬 실측)

- `ProfilePanel`(fileID `576258266`)의 부모 체인: `ContentArea`(fileID `1161641126`/`1161641127`) →
  `LobbyRoot`(fileID `694677461`/`694677462`) → `SafeAreaContainer`(fileID `435901785`/`435901786`,
  3888~3924행). **ProfilePanel은 SafeAreaContainer 하위에 정상 배치**(규칙 4 준수, fileID 추적 확인).
- `RankingPanel`도 동일한 `ContentArea`의 형제 자식이므로 같은 SafeAreaContainer 하위에 있다(아래 3장에서 재확인).
- CanvasGroup 규칙 5: `_anonymousSectionGroup`, `_changeNicknameButtonGroup` 모두 CanvasGroup 기반
  표시/숨김 사용(139~144, 165~170, 350~358, 536~544행) — 준수.
- 폰트: `CreateProfileStatsFields.cs`도 Light/Bold SDF 경로 로드 — 규칙 6 준수.

---

## 3. Ranking 탭 (Lobby.unity)

### 3-1. 레이아웃 문제

- `CreateRankingTable.cs` 100~104행: `ScrollView`에만 반투명 배경색(`(0.10, 0.12, 0.16, 0.85)`)이 `Image.color`로
  적용돼 있고, `HeaderRow`/`PaginationRow`에는 배경이 전혀 없다. 세 영역(`HeaderRow`/`ScrollView`/`PaginationRow`)을
  아우르는 **테이블 전체 패널 배경(`ui_panel_dark`/`ui_panel_light`)이 없다** — 확정 결정 4가 지적한 대로
  로비 배경 이미지(`bg_lobby`) 위에 테이블이 얹히면 시인성이 나쁠 것으로 판단된다.
- 헤더 열 6개(순위/닉네임/승률/게임수/승/패)는 `EnsureHLG(headerRow, 4f)`(89행)로 배치되며
  `childForceExpandWidth = true`(`EnsureHLG` 헬퍼 내부, 190~193행 상당)로 **모든 열이 동일 폭**을 갖는다.
  RankRow 프리팹의 6개 셀도 동일하게 `EnsureHLG(rowGo, 4f)`(238행)로 균등 폭이다. 닉네임처럼 긴 텍스트와
  순위처럼 1~2자리 숫자가 같은 폭을 차지해, 닉네임이 잘리거나 순위 열이 과도하게 넓어지는 문제가 예상된다
  (확정 결정 4 "열 폭 차등" 미구현).
- 정렬 방향 표시(▲▼) 없음: `OnHeaderClicked()`(256~287행)는 `_sortColumn`/`_sortAscending` 상태를 바꾸지만,
  헤더 버튼의 라벨 텍스트를 갱신하는 코드가 전혀 없다. `ColumnLabels` 배열(44행)은 `"순위", "닉네임", ...`
  고정 문자열이며 런타임에 변경되지 않는다 — 사용자는 현재 어떤 열 기준으로, 어떤 방향으로 정렬돼 있는지
  화면에서 알 수 없다.
- 짝수행 얼룩: `RankRowView.cs`에는 배경 `Image` 참조 자체가 없다(텍스트 6개 슬롯만 존재) — 얼룩 미구현.
  단, 확정 결정 4는 이를 "선택"으로 명시했으므로 규칙 위반은 아니다.

### 3-2. 프로세스 문제 — 새로고침 버튼 없음 (확정 결정 4 미구현)

- `RankingView.cs`에서 데이터 로드는 `Start()`(120행)와 `OnEnable()`(127~131행)뿐이다. Profile 탭과 동일하게
  탭 클릭 시 재로드되지 않으며(주석 20~24행에 이미 명시), 새로고침 버튼도 없다.

### 3-3. 프로세스 문제 — 빈 상태 안내 없음

- `RenderCurrentPage()`(348~371행): `_entries`가 비어 있어도(총게임수 20판 이상인 플레이어가 아무도 없는 경우)
  단순히 각 행을 `Clear()`하고 페이지 텍스트를 `"1 / 1"`로 표시할 뿐, "아직 랭킹 없음" 같은 안내 텍스트를
  표시하는 로직이 없다. GameSystemRules_UI.md Ranking 탭 규칙 5(총게임수 20 미만 미노출)와 결합하면, 서비스
  초기(플레이어 전원이 20판 미만)에는 랭킹 탭이 사실상 텅 빈 흰 테이블만 보이게 된다.

### 3-4. 규칙 준수 확인 (씬 실측)

- `RankingPanel`(fileID `1207351864`)의 부모 체인: `ContentArea`(fileID `1161641126`/`1161641127`, `ProfilePanel`과
  동일 부모) → `LobbyRoot` → `SafeAreaContainer`(fileID `435901785`). **RankingPanel도 SafeAreaContainer 하위에
  정상 배치**(규칙 4 준수, fileID 추적 확인).
- CanvasGroup 규칙 5: `RankRowView.SetVisible()`(103~111행)이 CanvasGroup 기반으로 표시/숨김 처리 — 준수.
- `RankRow.prefab`(`Assets/_Project/Prefabs/UI/RankRow.prefab`)은 이미 생성되어 있음(에디터 스크립트가 최소
  1회 실행된 상태) — 신규 프리팹 생성이 아니라 기존 프리팹 수정으로 접근해야 함.
- 폰트: `CreateRankingTable.cs`도 Light/Bold SDF 경로 로드 — 규칙 6 준수.

---

## 4. 공통 이슈

### 4-1. 스프라이트 전면 미사용 (확정 결정 5 위반)

세 에디터 스크립트의 `EnsureButton()`/`EnsureInputField()`/배경 생성 헬퍼는 전부 `Image.color`만 설정하고
`Image.sprite`를 대입하는 코드가 어디에도 없다(`CreateNicknameSetupPanel.cs` 223~237/243~284행,
`CreateProfileStatsFields.cs` 211~225행, `CreateRankingTable.cs` 349~363행 모두 동일 패턴). 세 화면 모두
단색 사각형 버튼/입력창/텍스트로만 구성돼 있어, 확정 결정 5("스프라이트는 적당한 것으로 적용")를 전혀
충족하지 못한다.

### 4-2. 필요 에셋 부재

`Assets/_Project/Sprites/UI/` 전체(116개 파일)를 검색한 결과 아래 두 종류의 아이콘이 존재하지 않는다.

| 필요 에셋 | 용도 | 현재 상태 |
|-----------|------|-----------|
| 새로고침 아이콘 | Profile 탭 "전적 새로고침" 버튼, Ranking 탭 새로고침 버튼 | **없음** — Icons 폴더에 back/email/logout/lock/gold/population/quit/rallypoint/randommatch/settings/singleplay/tab_battle/tab_profile/tab_ranking/tab_shop/timer/spinner_hexorb/destroy/createroom/customgame/joinbycode만 존재 |
| 정렬 화살표(▲▼) 아이콘 | Ranking 탭 헤더 열 정렬 방향 표시 | **없음** |

사용 가능 에셋(확인됨): `ui_panel_dark`/`ui_panel_light`(Panels), `ui_btn_gold`/`silver`/`sky`/`cancel`/`bronze`/
`tab` 등(Buttons), `ui_input_light`/`ui_input_dark`(Sprites/UI 루트), `ui_icon_back`/`email`/`logout`(Icons),
`bg_login`/`bg_lobby`(Backgrounds).

### 4-3. 범위 밖으로 확인된 기존 이슈 (참고용, 이번 작업 대상 아님)

- `LoginBootstrapper.cs`에 `[DEBUG-TEMP]` 주석이 붙은 `RuntimeLog()` 호출이 다수 남아있다(22, 158, 167, 171,
  330~344행). 이번 작업 범위 밖이므로 건드리지 않는다.

---

## 5. 다음 단계

이 감사 결과를 바탕으로 `Plan.md`에서 화면별 재설계 명세(레이아웃 도식, 사용 스프라이트, 프로세스/상태전이,
파일별 변경 내용, 아키텍처 제약 확인)를 작성한다.
