# Plan: 프로필 전적/랭킹 표시 및 닉네임 설정 구현

## 이 작업이 무엇인가

로그인 직후 닉네임을 처음 설정할 수 있는 화면을 추가하고,
로비 프로필 탭에 닉네임과 전적(승/패/승률)을 표시하며,
랭킹 탭에 플레이어 순위 테이블을 구현한다.

외부 라이브러리 없이 기존 프로젝트의 uGUI(CanvasGroup, ScrollRect, LayoutGroup) 패턴을 그대로 활용하며,
데이터는 UGS Cloud Save(전적/닉네임)와 UGS Leaderboard(랭킹)에서 가져온다.

---

## 전제 확인 사항 (구현 전 사용자 확인 필요)

UGS Cloud Save, Leaderboards, Cloud Code SDK가 Package Manager에 설치되어 있어야 한다.
또한 UGS Dashboard에서 아래 항목이 설정되어 있어야 한다:
- Cloud Save 키 스키마 (nickname, nicknameCode, totalGames, wins, losses, lastSessionEndAt, pendingGame, hasUsedFreeNicknameChange)
- Leaderboard ID 생성
- Cloud Code 함수 (recordMatchResult, initPlayer, checkPendingGame) 등록

---

## 구현 항목 상세

---

### A. Infrastructure 레이어 (신규)

#### A-1. `PlayerProfileService.cs`
**경로**: `Assets/_Project/Scripts/Infrastructure/Cloud/PlayerProfileService.cs`

UGS Cloud Save를 통해 플레이어 프로필 데이터를 읽고 쓰는 서비스.

```
기능:
- LoadProfileAsync() → nickname, nicknameCode, totalGames, wins, losses, lastSessionEndAt, hasUsedFreeNicknameChange 반환
- SaveNicknameAsync(nickname, code) → Cloud Save에 닉네임/코드 저장
- 반환 타입: PlayerProfileData (순수 C# 데이터 클래스)
```

**근거**: MEMORY.md — Application → Infrastructure 역방향 의존 금지. IPlayerProfileService 인터페이스를 Application 레이어에 선언하고 Infrastructure가 구현한다.

#### A-2. `LeaderboardService.cs`
**경로**: `Assets/_Project/Scripts/Infrastructure/Cloud/LeaderboardService.cs`

UGS Leaderboard를 통해 랭킹 데이터를 조회하는 서비스.

```
기능:
- GetTopRankingsAsync(limit) → LeaderboardEntry 목록 반환
- GetPlayerRankAsync(playerId) → 내 순위 반환
```

---

### B. Application 레이어 (신규)

#### B-1. `IPlayerProfileService.cs` (인터페이스)
**경로**: `Assets/_Project/Scripts/Application/Interfaces/IPlayerProfileService.cs`

Infrastructure 역참조 방지를 위한 인터페이스.
LoadProfileAsync(), SaveNicknameAsync() 선언.

**근거**: MEMORY.md — Application → Infrastructure 역참조 금지 → 의존성 역전 패턴 (인터페이스는 Application, 구현은 Infrastructure).

#### B-2. `ILeaderboardService.cs` (인터페이스)
**경로**: `Assets/_Project/Scripts/Application/Interfaces/ILeaderboardService.cs`

GetTopRankingsAsync(), GetPlayerRankAsync() 선언.

#### B-3. `PlayerProfileUseCase.cs`
**경로**: `Assets/_Project/Scripts/Application/UseCases/PlayerProfileUseCase.cs`

닉네임 초기화 / 조회 / 전적 조회를 담당하는 UseCase.

```
기능:
- LoadProfileAsync() → IPlayerProfileService를 통해 데이터 로드
- SaveNicknameAsync(nickname) → 닉네임 유효성 검증 후 저장
- GenerateAutoNickname(prefix) → 자동 닉네임 생성 (prefix + 임의 숫자/문자)
- IsFirstLogin() → Cloud Save에 nickname이 없으면 true
```

**근거**: GameSystemRules_UI.md 닉네임 설정 화면 규칙 2 (입력 검증은 클라이언트에서 1차 수행, 최종 저장은 서버). 규칙 3 (스킵 시 자동 생성 닉네임 형식).

#### B-4. `RankingUseCase.cs`
**경로**: `Assets/_Project/Scripts/Application/UseCases/RankingUseCase.cs`

랭킹 리스트 조회 + 내 순위 조회 UseCase.

```
기능:
- GetRankingsAsync() → LeaderboardEntry 목록
- GetMyRankAsync() → 내 순위 (20판 미만이면 -1 반환)
```

**근거**: GameSystemRules_UI.md Ranking 탭 UI 규칙 5 (총게임수 20 미만이면 랭킹 없음). 규칙 6 (Ranking 탭 활성화 시 로드).

---

### C. Presentation 레이어

#### C-1. `NicknameSetupView.cs` (신규)
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs`

Login.unity에 추가되는 닉네임 설정 패널 View.

```
Inspector 필드:
- TMP_InputField _nicknameInput
- Button _confirmButton
- Button _skipButton
- TextMeshProUGUI _statusText

기능:
- Initialize(rootView, profileUseCase, bootstrapper, isGooglePath) — 주입
- OnConfirmClicked() → 입력 검증 → SaveNicknameAsync → 다음 화면
- OnSkipClicked() → GenerateAutoNickname → SaveNicknameAsync → 다음 화면
- 다음 화면 분기: isGooglePath=true → GoToNextScene(), false → ShowEmailVerify()
```

**근거**: GameSystemRules_UI.md 닉네임 설정 화면 규칙 1 (화면 구성). 규칙 4 (완료 후 흐름). AuthSystemRules.md 닉네임 규칙 1~6.

#### C-2. `LoginRootView.cs` 수정
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`

- `LoginPanel` 열거형에 `NicknameSetup` 추가
- `[SerializeField] private CanvasGroup _nicknameSetupPanel` 필드 추가
- `ShowNicknameSetup()` 메서드 추가
- `HideAll()` / `SetActivePanel()` 스위치에 NicknameSetup 케이스 추가

**근거**: GameSystemRules_UI.md 공통 UI 규칙 5 (CanvasGroup 패턴 — 모든 패널 전환은 alpha/blocksRaycasts/interactable로 처리).

#### C-3. `LoginBootstrapper.cs` 수정
**경로**: `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs`

- `[SerializeField] private NicknameSetupView _nicknameSetupView` 필드 추가
- `InjectDependencies()` — NicknameSetupView 주입 추가
- `PlayerProfileUseCase` 인스턴스 생성 추가

#### C-4. `LoginSelectView.cs` 수정
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginSelectView.cs`

`OnGoogleLoginClicked()` 수정:
- 로그인 성공 후 `PlayerProfileUseCase.IsFirstLogin()` 호출
- 최초 로그인이면 `_rootView.ShowNicknameSetup()` (isGooglePath=true)
- 재로그인이면 `_bootstrapper.GoToNextScene()`

**근거**: AuthSystemRules.md Google 로그인 규칙 3 (최초 로그인 감지 → NicknameSetupView 표시).

#### C-5. `SignUpView.cs` 수정
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/SignUpView.cs`

이메일 회원가입 성공 후 흐름 변경:
- 기존: EmailVerifyView 바로 이동
- 변경: NicknameSetupView 표시 (isGooglePath=false) → 닉네임 설정 후 EmailVerifyView 이동

**근거**: AuthSystemRules.md 이메일 회원가입 규칙 4 (닉네임 설정 후 이메일 인증).

#### C-6. `ProfileView.cs` 수정
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs`

기존 Firebase DisplayName 표시 → Cloud Save 닉네임/전적 표시로 전환.

추가 Inspector 필드:
```
[Header("닉네임/코드")]
TextMeshProUGUI _nicknameText          → "닉네임#코드" 표시
Button _changeNicknameButton           → 닉네임 변경 버튼 (실계정만 표시)

[Header("전적")]
TextMeshProUGUI _totalGamesText        → 총게임수
TextMeshProUGUI _winsText              → 승
TextMeshProUGUI _lossesText            → 패
TextMeshProUGUI _winRateText           → 승률 (0판이면 "-")
TextMeshProUGUI _lastSessionText       → 마지막 접속종료

[Header("내 랭킹")]
TextMeshProUGUI _myRankText            → "N위" 또는 "순위 없음"
```

`RefreshUI()` 수정:
- `PlayerProfileUseCase.LoadProfileAsync()` 호출
- `RankingUseCase.GetMyRankAsync()` 호출
- 결과를 각 텍스트에 바인딩

`OnEnable()` — 탭 재진입 시 자동 갱신 (기존 패턴 유지)

**근거**: GameSystemRules_UI.md Profile 탭 UI 규칙 1 (레이아웃). 규칙 2 (닉네임 표시). 규칙 4 (전적). 규칙 5 (내 랭킹). 규칙 6 (탭 활성화 시 갱신).

#### C-7. `RankingView.cs` 전체 구현
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs`

현재 비어있는 파일을 전체 구현.

```
Inspector 필드:
- Transform _headerRow              → 헤더 행 부모 (Button 6개 자식)
- ScrollRect _scrollRect            → 스크롤 영역
- Transform _content                → VerticalLayoutGroup이 붙은 Content
- RankRowView _rowPrefab            → 행 프리팹 (10개 인스턴스 풀)
- Button _prevPageButton            → 이전 페이지
- Button _nextPageButton            → 다음 페이지
- TextMeshProUGUI _pageText         → "1 / 10"

기능:
- OnEnable() → LoadPageAsync(0)
- LoadPageAsync(page) → ShowLoading → RankingUseCase.GetRankingsAsync() → PopulateRows()
- PopulateRows(entries) → 10개 행에 데이터 바인딩
- OnHeaderClicked(column) → 정렬 전환 → 재정렬 후 갱신
- OnPrevPage() / OnNextPage() → 페이지 이동
```

**근거**: GameSystemRules_UI.md Ranking 탭 UI 규칙 1 (열 구성). 규칙 2 (10행 페이지네이션). 규칙 3 (열 정렬). 규칙 4 (ScrollRect + VerticalLayoutGroup 방식). 규칙 6 (탭 활성화 시 로드).

#### C-8. `RankRowView.cs` (신규)
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankRowView.cs`

랭킹 테이블 한 행을 표시하는 MonoBehaviour.

```
Inspector 필드:
- TextMeshProUGUI _rankText         → 순위
- TextMeshProUGUI _nicknameText     → 닉네임#코드
- TextMeshProUGUI _winRateText      → 승률
- TextMeshProUGUI _gamesText        → 게임수
- TextMeshProUGUI _winsText         → 승
- TextMeshProUGUI _lossesText       → 패

기능:
- Bind(LeaderboardEntry entry) → 각 텍스트에 바인딩
- Clear() → 빈 행 상태로 초기화 (페이지 나머지 행)
```

---

### D. Inspector 작업 (에디터 스크립트)

아래 항목은 에디터 스크립트로 자동 처리하거나 사용자 수동 작업이 필요하다.

1. **Login.unity** — NicknameSetupPanel 오브젝트 생성 및 NicknameSetupView 부착
2. **Login.unity** — LoginBootstrapper 슬롯에 NicknameSetupView 연결
3. **Login.unity** — LoginRootView의 `_nicknameSetupPanel` 슬롯에 CanvasGroup 연결
4. **Lobby.unity** — ProfileView에 신규 필드(닉네임, 전적 텍스트 등) 오브젝트 생성 및 연결
5. **Lobby.unity** — RankingPanel에 ScrollRect + VerticalLayoutGroup Content 구성
6. **Lobby.unity** — RankRowView 프리팹 생성 (HorizontalLayoutGroup으로 6열 구성)

---

## 아키텍처 제약 확인

| 제약 | 적용 방법 |
|------|-----------|
| Application → Infrastructure 역참조 금지 | IPlayerProfileService, ILeaderboardService 인터페이스를 Application에 선언, Infrastructure가 구현 |
| NetworkBehaviour는 Infrastructure에만 | Cloud Save/Leaderboard 서비스는 일반 C# 클래스로 구현 (MonoBehaviour 금지) |
| UIManager null-safe 패턴 | `UIManager.Instance?.ShowLoading(...)` 사용 |
| CanvasGroup 숨김/표시 | SetActive 대신 alpha/blocksRaycasts/interactable 사용 |
| 폰트 | Maplestory Light SDF (기본), Maplestory Bold SDF (헤더/강조) |

---

## 변경 파일 목록 (예상)

**[신규]**
- `Assets/_Project/Scripts/Infrastructure/Cloud/PlayerProfileService.cs`
- `Assets/_Project/Scripts/Infrastructure/Cloud/LeaderboardService.cs`
- `Assets/_Project/Scripts/Application/Interfaces/IPlayerProfileService.cs`
- `Assets/_Project/Scripts/Application/Interfaces/ILeaderboardService.cs`
- `Assets/_Project/Scripts/Application/UseCases/PlayerProfileUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/RankingUseCase.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankRowView.cs`

**[수정]**
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`
- `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginSelectView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/SignUpView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs`

---

## 위험 요소 및 주의사항

1. **UGS SDK 미설치 시 컴파일 오류**: Cloud Save, Leaderboards, Cloud Code SDK가 없으면 컴파일 자체가 안 된다. 구현 전 Package Manager에서 설치 여부 확인 필수.
2. **Leaderboard ID 불일치**: UGS Dashboard의 Leaderboard ID와 코드에서 사용하는 ID가 다르면 런타임 오류 발생.
3. **SignUpView 흐름 변경**: 기존에 이메일 가입 성공 → EmailVerify로 직행하던 흐름이 NicknameSetup을 거치도록 변경된다. 기존 테스트 케이스 재확인 필요.
4. **익명 계정 닉네임**: 익명 계정은 닉네임 설정 화면을 거치지 않으므로 Cloud Save에 nickname 키가 없을 수 있다. ProfileView에서 null 처리 필요.
