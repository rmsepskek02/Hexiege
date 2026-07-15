# Plan — 닉네임 설정 화면 + 프로필/랭킹 탭 UI 재작업

## 이 작업이 무엇을 하는가 (자연어 설명)

Research.md에서 확인한 문제들(뭉툭한 레이아웃, 스프라이트 미적용, 닉네임 설정 화면 뒤로가기 탈출 구멍,
새로고침 버튼 부재, 닉네임 변경 미구현, 랭킹 탭 열 폭 균등/빈 상태 안내 없음)을 사용자와 이미 확정한 5개
설계 결정에 맞춰 재설계하는 문서다. 이 문서는 **명세서**이며, 실제 코드 수정은 이후 game-programmer
에이전트가 이 문서를 보고 별도로 진행한다.

확정된 설계 결정(재확인, 변경 없음):
1. 닉네임 설정 화면 뒤로가기 완전 차단(Android 백버튼/UI 백 모두 무시).
2. 프로필 갱신은 "전적" 옆 새로고침 버튼으로만(탭 클릭 자동갱신 아님).
3. 닉네임 변경은 무료 1회(A안): 모달 팝업, 코드는 유지, 무료 소진 후 "다이아 N개 필요/구매하기(준비 중)" 안내만.
4. 랭킹은 페이지네이션 유지 + 새로고침 버튼 + 패널 배경(테이블을 패널 위에 배치) + 빈 상태 안내 + 정렬
   방향 화살표 + 열 폭 차등 + 짝수행 얼룩(선택).
5. 스프라이트는 적당한 것으로 적용 — 완벽한 픽셀 배치보다 "규칙 준수 + 스프라이트 적용 + 슬롯 연결"이 목표.

---

## 1. 닉네임 설정 화면 재설계

### 1-1. 레이아웃

```
NicknameSetupPanel (CanvasGroup, Image=ui_panel_dark, Type=Sliced)
└─ Content (앵커 15~85% x, 20~80% y — 기존 유지)
   └─ VerticalLayoutGroup (childForceExpandHeight=false)
      ├─ Title            [LayoutElement: preferredHeight=80,  flexibleHeight=0]
      ├─ NicknameInput    [LayoutElement: preferredHeight=90,  flexibleHeight=0] (Image=ui_input_light, Sliced)
      ├─ ConfirmButton    [LayoutElement: preferredHeight=90,  flexibleHeight=0] (Image=ui_btn_gold, Sliced)
      ├─ SkipButton       [LayoutElement: preferredHeight=70,  flexibleHeight=0] (Image=ui_btn_silver, Sliced)
      └─ StatusText       [LayoutElement: preferredHeight=0,   flexibleHeight=1]  (남는 공간 흡수)
```

- 근거: 공통 UI 규칙 2(고정 픽셀 대신 비율/LayoutElement 가중치), `.claude/agent-memory` 교훈
  "ChildForceExpandHeight만으론 부족 → LayoutElement.preferredHeight + flexibleHeight 비율 가중치".
- 사용 스프라이트: 배경 `ui_panel_dark`, 입력창 `ui_input_light`, 확인 버튼 `ui_btn_gold`, 스킵 버튼 `ui_btn_silver`.
  (`ui_panel_light`/`ui_btn_cancel` 등으로 교체 가능 — 사용자가 실기에서 미세조절 예정, 확정 결정 5.)

### 1-2. 프로세스 — 뒤로가기 완전 차단 (확정 결정 1)

- `LoginRootView.HandleBack()` 최상단에 분기 추가:
  ```
  if (_currentPanel == LoginPanel.NicknameSetup)
      return; // 필수 통과 화면 — Android 백버튼/UI 백 모두 무시
  ```
- Android 백버튼과 UI 백 버튼이 모두 `HandleBack()` 한 곳을 경유하므로(Update()의 Escape 감지 +
  향후 추가될 UI 백버튼 모두 `HandleBack()` 호출), 이 한 줄로 두 경로를 동시에 차단한다.
- 별도의 안내 문구/토스트는 넣지 않는다 — 확정 결정 1은 "무시"만 요구하며, Login 씬에는 재사용 가능한
  전역 토스트 시스템이 확인되지 않아(생산 패널 UI 규칙 25~28의 토스트는 인게임 시스템, Login 씬과 무관)
  새 UI를 추가하는 것은 범위 확장이 된다.
- 근거: 확정 결정 1(사용자 승인 사항), GameSystemRules_UI.md 닉네임 설정 화면 규칙 4("완료 후에는 경로와
  무관하게 항상 로비로 이동" — 즉 이 화면은 반드시 거쳐야 하는 게이트임을 문서가 이미 전제하고 있음).
  **문서 보완 필요**: GameSystemRules_UI.md 닉네임 설정 화면 섹션에 "규칙 5. 뒤로가기 차단"을 명문화하는
  것을 권장한다(이 Plan의 구현 대상은 아니며, WORKFLOW [12] 단계에서 document-manager가 처리할 사안).

### 1-3. 프로세스 — 확인 버튼 실시간 비활성화

- `NicknameSetupView.Initialize()`에서 `_nicknameInput.onValueChanged.AddListener(OnInputChanged)` 추가.
- `OnInputChanged(string text)`: `_profileUseCase.ValidateNickname(text)` 결과가 `Valid`가 아니면
  `_confirmButton.interactable = false`, 유효하면 `true`.
- `PrepareForShow()`에서 입력 필드를 비울 때 `_confirmButton.interactable = false`로 초기화(빈 값이므로).
- 근거: GameSystemRules_UI.md 닉네임 설정 화면 규칙 2("빈 값이면 확인 버튼 클릭 불가 **또는** 안내 메시지
  표시" 중 "클릭 불가" 방식을 채택). 기존 안내 메시지(`SetStatus`)는 저장 실패(네트워크 오류 등) 시 그대로 유지.

---

## 2. Profile 탭 재설계

### 2-1. 레이아웃 — 두 트리 통합

Research.md 2-1에서 확인한 `LobbyProfileView`(기존 수작업 UI)와 `ProfileStatsContainer`(에디터 생성)의
겹침을 해소하기 위해, `ProfileStatsContainer`를 `LobbyProfileView/MainView` 하위로 재배치하고 전체를
하나의 `VerticalLayoutGroup`으로 통합한다.

```
ProfilePanel
└─ LobbyProfileView (0~1 스트레치, 기존 유지)
    ├─ BackButton (기존 유지)
    └─ MainView (0~1 스트레치)
        └─ VerticalLayoutGroup (신규 통합, childForceExpandHeight=false)
            ├─ AccountInfoSection    [기존 _accountInfoText — 슬롯 유지, LayoutElement preferredHeight=120]
            ├─ NicknameSection       [닉네임#코드 + 변경버튼 — LayoutElement preferredHeight=90]
            ├─ StatsSection          [총게임/승/패/승률/마지막접속 + 새로고침 버튼 — flexibleHeight=1]
            ├─ MyRankSection         [내 랭킹 — LayoutElement preferredHeight=60]
            ├─ AnonymousSection      [기존 _anonymousSection — CanvasGroup 유지]
            └─ LogoutButton          [기존 유지]
    └─ SubViewContainer (기존 유지)
```

- 근거: Profile 탭 UI 규칙 1(레이아웃 구성 순서: 계정정보 → 닉네임/전적 → 내랭킹 → 계정연동 → 로그아웃).
- 스프라이트: 새로고침 버튼 `ui_btn_sky`, 닉네임 변경 버튼 `ui_btn_gold`, 로그아웃/연동 버튼은 기존 스프라이트
  유지(이번 작업 범위 아님).

### 2-2. 프로세스 — 새로고침 버튼 (확정 결정 2)

- "전적" 섹션 헤더 옆에 새로고침 버튼(`_refreshButton`) 신규 배치. 아이콘 없으므로(Research 4-2) 우선
  텍스트 라벨 `"새로고침"` 또는 유니코드 기호 `"⟳"`로 대체하고, 추후 전용 아이콘 확보 시 교체한다.
- `ProfileView.cs`에 `_refreshButton` 슬롯 추가, `Start()`에서 `_refreshButton.onClick.AddListener(OnRefreshClicked)`.
- `OnRefreshClicked()`: `_ = RefreshProfileDataAsync()` 호출(기존 메서드 재사용 — 전적 + 내 랭킹을 함께 재로드).
  `UIManager.Instance?.ShowLoading(true/false, "갱신 중...")`로 감싸 사용자에게 진행 중임을 알린다(규칙 L-4
  null-safe 패턴 준수).
- OnEnable() 기반 자동 갱신은 유지하되(씬 진입 시 1회), "탭 클릭 시 자동 갱신"은 추가하지 않는다(확정 결정 2).
- 근거: 확정 결정 2, Profile 탭 UI 규칙 6("Profile 탭이 활성화될 때마다 최신 데이터를 로드" — 이 규칙은
  OnEnable 1회 로드로 이미 충족되며, 새로고침 버튼은 그 위에 추가되는 수동 갱신 수단).

### 2-3. 프로세스 — 닉네임 변경 모달 (확정 결정 3)

**신규 View**: `NicknameChangePopup.cs` (Presentation, Lobby 씬)

```
NicknameChangePopup (모달, GameSystemRules_UI 규칙 8~9: 배경 탭으로 닫히지 않음)
├─ BlockingOverlay (UIManager.ShowBlockingOverlay() 재사용 — 신규 오버레이 생성 안 함)
├─ Panel (Image=ui_panel_dark)
│   ├─ TitleText ("닉네임 변경")
│   ├─ [무료 미사용 시] NicknameInput (Image=ui_input_light) + StatusText(검증 실패 안내)
│   ├─ [무료 소진 시]   PaidNoticeText ("다이아 N개 필요") + PurchaseButton("구매하기") — 클릭 시 토스트/상태
│   │                    텍스트로 "준비 중" 안내만(결제 미구현, UGS Economy 후속)
│   ├─ ConfirmButton (Image=ui_btn_gold)
│   └─ CancelButton  (Image=ui_btn_cancel)
```

- `ProfileView.OnChangeNicknameClicked()`를 수정: `LoadProfileAsync()`로 `hasUsedFreeNicknameChange` 조회 후
  `_nicknameChangePopup.Show(usedFree, currentNickname)` 호출로 교체(기존의 `SetStatus()` 안내 텍스트 방식 제거).
- **확인 버튼 클릭(무료 미사용 케이스)** 흐름:
  1. `PlayerProfileUseCase.ValidateNickname(input)` 클라이언트 검증.
  2. 통과 시 `PlayerProfileUseCase.ChangeNicknameAsync(input)` 호출(신규 메서드, 아래 2-4 참조).
  3. 성공 시 팝업 닫기 + `ProfileView.RefreshProfileDataAsync()` 재호출(확정 결정 3 "저장 + 프로필 갱신").
- **무료 소진 케이스**: 입력 필드 대신 안내문 + "구매하기(준비 중)" 버튼만 표시. 버튼 클릭 시 토스트/상태
  텍스트로 "준비 중" 표시만 하고 팝업은 유지(사용자가 다시 취소를 눌러야 닫힘 — 모달이므로 배경 탭 닫기 불가,
  규칙 9).
- 근거: 확정 결정 3, GameSystemRules_UI.md 규칙 8("모달 = 명시적 Y/N 또는 확정 조작이 필요한 팝업"),
  규칙 9("모달은 배경 탭 닫기 불가, 확인/취소로만 닫힘"), Profile 탭 UI 규칙 3("hasUsedFreeNicknameChange
  값에 따라 무료/유료 안내 구분").

### 2-4. Application/Infrastructure 변경 — 코드 유지 + 플래그 저장

**문제**: Research 2-3에서 확인했듯 (a) `PlayerProfileUseCase.SaveNicknameAsync()`는 호출마다 새 코드를
생성하므로 그대로 쓰면 코드가 바뀐다. (b) `IPlayerProfileService.SaveNicknameAsync(nickname, code)`에는
`hasUsedFreeNicknameChange`를 저장하는 파라미터가 없다.

**변경 1 — `IPlayerProfileService.cs` (Application 레이어, 인터페이스)**:
```csharp
// 기존 시그니처 유지 + 신규 오버로드 추가(하위 호환)
Task SaveNicknameAsync(string nickname, string code);
Task SaveNicknameAsync(string nickname, string code, bool hasUsedFreeNicknameChange); // 신규
```

**변경 2 — `PlayerProfileService.cs` (Infrastructure, 구현체)**:
- 신규 오버로드 구현: 기존 `SaveNicknameAsync(nickname, code)` 로직에 `KeyHasUsedFreeNicknameChange` 키를
  dictionary에 추가해 `CloudSaveService.Instance.Data.Player.SaveAsync()` 한 번에 같이 저장.
- 근거: `PlayerProfileService.cs` 55행에 상수는 이미 있으나(Research 2-3) 저장 로직에서 쓰인 적이 없음 —
  이번에 처음 실제로 사용.

**변경 3 — `PlayerProfileUseCase.cs` (Application, UseCase)**: 신규 메서드
```csharp
public async Task<NicknameValidation> ChangeNicknameAsync(string newNickname)
{
    NicknameValidation validation = ValidateNickname(newNickname);
    if (validation != NicknameValidation.Valid)
        return validation;

    // 기존 코드를 유지해야 하므로(확정 결정 3), 먼저 현재 프로필을 로드해 코드값을 재사용한다.
    PlayerProfileData current = await _profileService.LoadProfileAsync();
    string existingCode = current != null ? current.NicknameCode : GenerateCode();

    await _profileService.SaveNicknameAsync(newNickname.Trim(), existingCode, hasUsedFreeNicknameChange: true);
    return NicknameValidation.Valid;
}
```
- 이 메서드는 **무료 1회 변경 케이스에서만** 호출한다(무료 소진 여부 판정은 `NicknameChangePopup`이
  `LoadProfileAsync()`로 미리 확인 후 무료 UI를 노출할지 결정하므로, `ChangeNicknameAsync` 자체는 방어적으로
  한 번 더 `hasUsedFreeNicknameChange`를 재확인하지 않는다 — 서버(Cloud Code) 강제는 후속 과제로 범위 밖).
- 근거: 확정 결정 3("코드는 변경해도 유지", "저장 + hasUsedFreeNicknameChange=true"), Application →
  Infrastructure 역참조 금지 제약(MEMORY.md) — `ChangeNicknameAsync`는 `IPlayerProfileService` 인터페이스만
  사용하므로 준수.

---

## 3. Ranking 탭 재설계

### 3-1. 레이아웃 — 패널 배경 + 열 폭 차등

- `RankingTable`(전체 컨테이너) GameObject에 `Image` 추가, `sprite = ui_panel_dark`(또는 `ui_panel_light`),
  `Type = Sliced`. 기존 `ScrollView`의 단색 배경(`Image.color`)은 패널이 이미 배경을 제공하므로 제거하거나
  투명하게 낮춘다.
- 6개 열(헤더 + 행 셀 공통)에 `LayoutElement.flexibleWidth` 비율 지정(고정 픽셀 대신 비율 — 공통 UI 규칙 2):

| 열 | flexibleWidth |
|----|---------------|
| 순위 | 0.6 |
| 닉네임 | 2.0 |
| 승률 | 1.0 |
| 게임수 | 1.0 |
| 승 | 0.8 |
| 패 | 0.8 |

- `HorizontalLayoutGroup.childForceExpandWidth = true`는 유지하되, 각 자식(열)에 위 비율의 `LayoutElement`를
  추가해 폭을 차등화한다(균등폭이었던 기존 `EnsureHLG` 동작 위에 `LayoutElement`를 얹어 오버라이드).
- 헤더와 행(`RankRowView` 프리팹)에 **동일한 비율**을 적용해 열이 어긋나지 않도록 한다.

### 3-2. 프로세스 — 새로고침 버튼 (확정 결정 4)

- `PaginationRow` 옆(또는 `HeaderRow` 우측)에 새로고침 버튼(`_refreshButton`) 신규 배치. 아이콘 부재로
  Profile 탭과 동일하게 텍스트 `"새로고침"`/`"⟳"`로 대체.
- `RankingView.cs`에 `_refreshButton` 슬롯 추가, `InitializeIfNeeded()`에서 리스너 등록,
  클릭 시 `_ = RefreshAsync()`(기존 메서드 재사용).

### 3-3. 프로세스 — 빈 상태 안내

- `RankingView.cs`에 `_emptyStateText`(TextMeshProUGUI) 슬롯 신규 추가.
- `RefreshAsync()` 완료 후 `_entries.Count == 0`이면:
  - `_emptyStateText.text = "아직 랭킹 없음 (20판 이상 필요)"`, CanvasGroup(또는 `gameObject.SetActive`가
    아닌 별도 표시 로직 — 규칙 5)으로 표시.
  - `ScrollView`(행 목록)와 `PaginationRow`는 CanvasGroup으로 숨김(alpha=0).
- `_entries.Count > 0`이면 반대로 처리.
- 근거: 확정 결정 4, Ranking 탭 UI 규칙 5(20판 미만 미노출) — 노출 대상이 아무도 없을 때의 사용자 피드백은
  문서에 없던 공백이었으므로 이번에 보완.

### 3-4. 프로세스 — 정렬 방향 화살표

- `RankingView.cs`에 `UpdateHeaderLabels()` 신규 private 메서드 추가: `_headerButtons` 순회하며 현재
  `_sortColumn`에 대응하는 버튼에만 라벨 텍스트를 `"{원래 라벨} {화살표}"`(화살표: `_sortAscending ? "▲" : "▼"`)로
  갱신, 나머지는 원래 라벨로 되돌림.
- `OnHeaderClicked()`와 `RefreshAsync()`(정렬 초기화 시점) 양쪽에서 `ApplySort()` 직후 `UpdateHeaderLabels()` 호출.
- 헤더 버튼의 원본 라벨은 `CreateRankingTable.cs`의 `ColumnLabels` 배열과 동일한 순서로 `RankingView`에도
  캐시(`private static readonly string[] _columnLabels = {"순위","닉네임","승률","게임수","승","패"};`)해
  갱신 시 사용한다.

### 3-5. 짝수행 얼룩 (선택, 확정 결정 4)

- `RankRowView.cs`에 `Image` 배경 참조(`_rowBackground`) 신규 슬롯 추가.
- `RankRowView.Bind()`에 `int rowIndex` 파라미터 추가(오버로드 또는 시그니처 확장), 짝수 인덱스면
  `_rowBackground.color = (밝은 회색, 낮은 알파)`, 홀수면 투명/기본색.
- `RankingView.RenderCurrentPage()`에서 `_rows[i].Bind(_entries[dataIndex], i)` 형태로 인덱스 전달.
- 이 항목은 확정 결정 4에서 "선택"으로 명시했으므로, 우선순위는 3-1~3-4보다 낮게 잡는다.

---

## 4. 구현 항목 분류

### 4-1. 에디터 스크립트 수정

| 파일 | 변경 내용 | 근거 |
|------|-----------|------|
| `CreateNicknameSetupPanel.cs` | 배경 Image에 `ui_panel_dark` sprite 적용, VLG `childForceExpandHeight=false`+자식별 `LayoutElement` 추가, 버튼에 `ui_btn_gold`/`ui_btn_silver` sprite 적용, 입력창 배경에 `ui_input_light` sprite 적용 | 공통 UI 규칙 2, 확정 결정 5 |
| `CreateProfileStatsFields.cs` | 생성 위치를 `MainView` 하위로 변경(부모 탐색 로직 수정), 통합 VLG로 재구성(`childForceExpandHeight=false`+`LayoutElement`), 새로고침 버튼(`_refreshButton`) 신규 생성+연결, 닉네임 변경 버튼에 `ui_btn_gold` sprite 적용 | Profile 탭 UI 규칙 1, 확정 결정 2·5 |
| `CreateRankingTable.cs` | `RankingTable`에 `ui_panel_dark` 배경 Image 추가, 헤더/행 셀에 `LayoutElement.flexibleWidth` 비율 적용, 새로고침 버튼(`_refreshButton`)·빈상태 텍스트(`_emptyStateText`) 신규 생성+연결, RankRow 프리팹에 배경 `Image`(`_rowBackground`) 추가(선택) | Ranking 탭 UI 규칙 1·2, 확정 결정 4·5 |

### 4-2. View 코드 수정

| 파일 | 변경 내용 | 근거 |
|------|-----------|------|
| `NicknameSetupView.cs` | `onValueChanged` 리스너로 확인 버튼 실시간 활성/비활성, `PrepareForShow()` 초기 비활성화 | 닉네임 설정 화면 규칙 2, 확정 결정(구현 명세) |
| `LoginRootView.cs` | `HandleBack()` 최상단에 `NicknameSetup` 패널 무시 분기 추가 | 확정 결정 1 |
| `ProfileView.cs` | `_refreshButton`/`_nicknameChangePopup` 슬롯 추가, `OnChangeNicknameClicked()`를 모달 오픈 방식으로 교체, `OnRefreshClicked()` 신규 | Profile 탭 UI 규칙 1·3·6, 확정 결정 2·3 |
| `RankingView.cs` | `_refreshButton`/`_emptyStateText` 슬롯 추가, `OnRefreshClicked()`/`UpdateHeaderLabels()`/빈상태 토글 로직 신규 | Ranking 탭 UI 규칙 2·3·5, 확정 결정 4 |
| `RankRowView.cs` | `_rowBackground` 슬롯 추가, `Bind()`에 `rowIndex` 파라미터 확장(선택) | 확정 결정 4(선택) |
| `PlayerProfileUseCase.cs` | `ChangeNicknameAsync(string)` 신규 메서드 | 확정 결정 3, Application→Infrastructure 역참조 금지 준수 |
| `IPlayerProfileService.cs` | `SaveNicknameAsync(nickname, code, hasUsedFreeNicknameChange)` 오버로드 추가 | 확정 결정 3, 의존성 역전 패턴(MEMORY.md) |
| `PlayerProfileService.cs` | 위 오버로드 구현(Cloud Save에 `hasUsedFreeNicknameChange` 저장) | 확정 결정 3 |

### 4-3. 신규 파일

| 파일 | 역할 | 근거 |
|------|------|------|
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs` | 닉네임 변경 모달 View | 확정 결정 3, GameSystemRules_UI 규칙 8·9 |
| `Assets/Editor/Setup/CreateNicknameChangePopup.cs` | Lobby 씬에 위 팝업 GameObject 생성 + 슬롯 연결(1회성 에디터 스크립트, 기존 3종과 동일 패턴) | WORKFLOW [5-2] Inspector 작업 규칙 |

---

## 5. 아키텍처 제약 확인 표

| 제약 | 확인 결과 |
|------|-----------|
| Application → Infrastructure 역참조 금지 | `ChangeNicknameAsync`는 `IPlayerProfileService`(Application 인터페이스)만 호출 — 준수. `PlayerProfileService`(구현)는 Infrastructure에 위치, 인터페이스 구현으로 의존성 역전 유지 |
| Presentation → Infrastructure 참조 | `NicknameChangePopup`은 신규 Infrastructure 클래스를 만들지 않고 `ProfileView`가 이미 보유한 `PlayerProfileUseCase` 인스턴스를 주입받아 사용 — 기존 패턴과 동일 |
| CanvasGroup 규칙 5 | 모든 신규 표시/숨김(빈 상태 안내, 짝수행, 모달)은 `SetActive` 대신 CanvasGroup alpha/blocksRaycasts/interactable 사용 |
| UIManager null-safe | 새로고침 로딩 표시, 모달 배경 오버레이 모두 `UIManager.Instance?.` 패턴 사용(씬 직접 진입 대비) |
| BlockingOverlay 단일 소유 | `NicknameChangePopup`은 자체 오버레이를 만들지 않고 `UIManager.ShowBlockingOverlay()`(Modal 모드, 콜백 없음)를 재사용 |
| 폰트 규칙 6 | 모든 신규 텍스트는 Maplestory Light/Bold SDF만 사용 |
| SafeAreaContainer 규칙 4 | `NicknameChangePopup`도 Lobby 씬 `SafeAreaContainer`(fileID `435901785`) 하위, 구체적으로는 `ProfilePanel`과 같은 `ContentArea` 하위에 배치해야 함 |
| Assembly Definitions 없음 | 네임스페이스 규약만으로 레이어 구분 — 신규 파일도 기존 네임스페이스 규약(`Hexiege.Presentation`/`Hexiege.Application`) 그대로 따름 |

---

## 6. 변경/신규 파일 목록

```
[신규]
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs
- Assets/Editor/Setup/CreateNicknameChangePopup.cs

[수정]
- Assets/Editor/Setup/CreateNicknameSetupPanel.cs
- Assets/Editor/Setup/CreateProfileStatsFields.cs
- Assets/Editor/Setup/CreateRankingTable.cs
- Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs
- Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankRowView.cs (선택 — 짝수행 얼룩)
- Assets/_Project/Scripts/Application/UseCases/PlayerProfileUseCase.cs
- Assets/_Project/Scripts/Application/Interfaces/IPlayerProfileService.cs
- Assets/_Project/Scripts/Infrastructure/Cloud/PlayerProfileService.cs

[씬 — Inspector 작업 필요, 5-2 단계]
- Assets/_Project/Scenes/Login.unity (NicknameSetupPanel 재구성 — 에디터 스크립트 재실행)
- Assets/_Project/Scenes/Lobby.unity (ProfileStatsContainer 재배치, RankingTable 배경/열폭, NicknameChangePopup 신규 배치)

[프리팹]
- Assets/_Project/Prefabs/UI/RankRow.prefab (열 폭 LayoutElement, 배경 Image 추가 — 기존 프리팹 수정)
```

---

## 7. 실기 테스트 시나리오 (참고용 — Testcase.md 작성은 사용자 지시 시 별도 진행)

1. 닉네임 설정 화면에서 Android 백버튼(에디터는 ESC)을 여러 번 눌러도 화면이 유지되는지.
2. 닉네임 입력창이 비어 있을 때 확인 버튼이 비활성 상태인지, 유효한 값을 입력하면 활성화되는지.
3. Profile 탭에서 새로고침 버튼을 눌렀을 때 전적과 내 랭킹이 갱신되는지(탭 재진입 없이).
4. 닉네임 변경 모달 — 무료 미사용 상태에서 새 닉네임 저장 후 코드(#4729 등)가 그대로 유지되는지, 프로필이
   자동 갱신되는지.
5. 닉네임 변경 모달 — 무료 소진 상태(hasUsedFreeNicknameChange=true)에서 입력창 대신 "다이아 필요/구매하기"
   안내만 나오는지.
6. 닉네임 변경 모달 — 배경을 탭해도 닫히지 않는지(모달 규칙 9), 취소 버튼으로만 닫히는지.
7. 랭킹 탭 — 새로고침 버튼 클릭 시 목록이 갱신되는지.
8. 랭킹 탭 — 등재 인원이 0명일 때 빈 상태 안내 문구가 표시되는지.
9. 랭킹 탭 — 헤더 열 클릭 시 정렬 방향 화살표가 갱신되는지, 페이지 이동이 여전히 정상 동작하는지.
10. 랭킹 탭 — 닉네임 열이 순위/승/패 열보다 넓게 표시되는지(잘림 없이).

---

## 8. 범위 밖 (명시)

- 서버(UGS Cloud Code) 연동: `initPlayer`, `recordMatchResult` 등 서버 측 API 연결은 이번 작업 대상이 아니다.
- 닉네임 변경 유료 결제(UGS Economy 실제 구매 흐름)는 미구축 상태이며, 이번 작업에서는 "준비 중" 안내까지만
  구현한다. 무료 1회 서버 강제(Cloud Code에서 hasUsedFreeNicknameChange를 서버가 검증)도 후속 과제다.
- `LoginBootstrapper.cs`의 `[DEBUG-TEMP]` 로그 제거는 이번 작업 범위가 아니다(Research 4-3 참고, 별도 작업).
- GameSystemRules_UI.md 문서 자체의 규칙 추가/수정(1-2절에서 언급한 "닉네임 설정 화면 규칙 5" 등)은 이
  Plan의 구현 대상이 아니며, WORKFLOW [12] 단계에서 document-manager가 별도로 처리한다.
