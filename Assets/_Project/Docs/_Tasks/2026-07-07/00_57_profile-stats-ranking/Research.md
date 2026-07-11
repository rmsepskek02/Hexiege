# Research: 프로필 전적/랭킹 표시 및 닉네임 설정 구현

## 이 작업이 무엇인가

로그인 이후 플레이어의 **닉네임을 처음 설정**하는 화면을 추가하고,
로비의 **프로필 탭에 닉네임과 전적(승/패/승률)** 을 표시하며,
**랭킹 탭에 플레이어 순위 테이블**을 구현하는 작업이다.

데이터는 UGS Cloud Save(닉네임/전적)와 UGS Leaderboard(랭킹)에서 가져오며,
경기 결과는 UGS Cloud Code를 통해 서버에서 기록한다.
UI는 기존 프로젝트의 uGUI(CanvasGroup) 패턴을 그대로 활용하고,
랭킹 테이블은 외부 라이브러리 없이 커스텀 uGUI로 직접 구성한다.

---

## 현재 코드 상태 분석

### 1. Login 씬 — 닉네임 설정 패널 없음

**LoginRootView.cs**
- `LoginPanel` 열거형: `None, LoginSelect, EmailLogin, SignUp, EmailVerify, PasswordReset`
  - `NicknameSetup` 없음 → 추가 필요
- `_nicknameSetupPanel` CanvasGroup 필드 없음
- `ShowNicknameSetup()` 메서드 없음
- 패턴: `ShowGroup()` / `HideGroup()` + `SetActivePanel()` 스위치 방식

**LoginBootstrapper.cs**
- `InjectDependencies()` — NicknameSetupView 주입 없음
- `GoToNextScene()` — 닉네임 확인 없이 Lobby 바로 이동
- 현재 의존성 주입 목록: LoginSelectView, EmailLoginView, SignUpView, EmailVerifyView, PasswordResetView, AnonymousWarningPopup, NetworkErrorPopup

**LoginSelectView.cs**
- `OnGoogleLoginClicked()` → 성공 시 `_bootstrapper.GoToNextScene()` 직접 호출
  - 최초 로그인 여부 체크 없음 → NicknameSetup 분기 없음

### 2. Lobby 씬 — ProfileView 전적/닉네임 없음

**ProfileView.cs**
- 현재 표시: Firebase `DisplayName` 또는 `Email` — UGS Cloud Save 닉네임 아님
- 전적 없음 (totalGames, wins, losses 표시 없음)
- 랭킹 순위 없음
- Cloud Save 연동 없음 (FirebaseAuthService만 사용)
- `OnEnable()` 에서 `RefreshUI()` 호출 — 탭 재진입 시 갱신하는 구조는 이미 있음

**LobbyRootView.cs**
- ProfilePanel, RankingPanel 모두 CanvasGroup 기반으로 탭 전환 이미 구현됨
- `LobbyViewModel.CurrentTab` 구독으로 패널 표시/숨김 처리

**LobbyViewModel.cs**
- `LobbyTab` 열거값: `Battle, Shop, Profile, Setting, Ranking` (5탭)
  - **[2026-07-09 main 병합 반영]** 기존 4탭(Battle/Shop/Profile/Ranking)에 **Setting 탭 신규 추가**됨
  - Profile 탭과 Setting 탭이 **완전히 분리된 별도 최상위 탭**으로 구성됨 (GameSystemRules_UI.md "로비 설정/프로필 UI" 규칙 1)
  - Profile 탭 = 전적/계정 관리, Setting 탭 = 사운드 등 게임 설정
- `LobbyTab.Ranking`, `LobbyTab.Profile` 모두 정의됨 — 본 작업의 타겟 탭
- UniRx `ReactiveProperty`, `Subject` 패턴 사용
- **본 작업은 LobbyViewModel을 수정하지 않음** (Setting 탭은 main에서 이미 추가 완료)

### 3. Lobby 씬 — RankingView 완전 비어있음

**RankingView.cs**
```csharp
public class RankingView : MonoBehaviour
{
    /* 추후 구현 예정 */
}
```
- 아무 로직도 없음 → 전체 구현 필요

### 4. UGS 인프라 현황

**UnityServicesInitializer.cs**
- UGS SDK 초기화(`UnityServices.InitializeAsync()`) 구현됨
- OIDC Bridge(Firebase → UGS PlayerId 연결) 구현됨
- Cloud Save, Leaderboard, Cloud Code 연동 코드는 **없음**

**기존 보유 UGS SDK 패키지** (LobbyManager, RelayManager 등에서 이미 사용)
- `Unity.Services.Core`, `Unity.Services.Authentication` 사용 확인
- `Unity.Services.CloudSave`, `Unity.Services.Leaderboards`, `Unity.Services.CloudCode`가 패키지에 포함되어 있는지는 Package Manager 확인 필요

---

## 기존 보유 에셋 & 패턴 (최대한 활용할 것)

| 에셋/패턴 | 현황 | 적용 방법 |
|---|---|---|
| **CanvasGroup 표시/숨김** | 전 UI에서 공통 사용 | NicknameSetupView, ProfileView 섹션, RankingView 동일 적용 |
| **`Initialize(deps...)` 주입 패턴** | LoginSelectView 등 전 Login View | NicknameSetupView 동일 적용 |
| **`async void` + try/finally 버튼 핸들러** | LoginSelectView, ProfileView | NicknameSetupView, ProfileView 수정 시 동일 적용 |
| **`UIManager.Instance?.ShowLoading()`** | 전 View 공통 | 닉네임 저장, 데이터 로드 시 동일 적용 |
| **TextMeshPro** | 전 UI 텍스트 | 닉네임 표시, 전적, 랭킹 테이블 텍스트 |
| **UniRx ReactiveProperty/Subject** | LobbyViewModel | RankingViewModel, ProfileViewModel |
| **ScrollRect** | 기존 씬에서 사용 | RankingView 스크롤 리스트 |
| **HorizontalLayoutGroup** | 기존 씬에서 사용 | 랭킹 테이블 각 행(Row) 구성 |
| **VerticalLayoutGroup** | 기존 씬에서 사용 | 랭킹 테이블 Content 구성 |

---

## 구현 필요 범위 요약

### Infrastructure 레이어 (신규)
- `PlayerProfileService.cs` — UGS Cloud Save CRUD (닉네임, 코드, 전적)
- `LeaderboardService.cs` — UGS Leaderboard 조회

### Application 레이어 (신규)
- `PlayerProfileUseCase.cs` — 닉네임 초기화/조회/변경, 전적 조회
- `RankingUseCase.cs` — 랭킹 리스트 조회

### Presentation 레이어 (신규/수정)
- `NicknameSetupView.cs` ← 신규
- `ProfileView.cs` ← 기존 수정 (닉네임+전적 섹션 추가)
- `RankingView.cs` ← 기존 비어있음, 전체 구현
- `RankRowView.cs` ← 신규 (랭킹 테이블 행 프리팹용)
- `RankingViewModel.cs` ← 신규
- `ProfileViewModel.cs` ← 신규 (또는 ProfileView 내부 처리)

### Bootstrap 레이어 (수정)
- `LoginRootView.cs` — NicknameSetup 패널 추가
- `LoginBootstrapper.cs` — NicknameSetupView 주입 추가

---

## 확인 필요 사항

1. **UGS SDK 패키지 포함 여부**: Cloud Save / Leaderboards / Cloud Code가 이미 설치됐는지 Package Manager에서 확인
2. **UGS Dashboard 설정**: Cloud Code 함수(`recordMatchResult`, `initPlayer`, `checkPendingGame`) 등록 여부
3. **UGS Leaderboard 생성 여부**: Dashboard에서 Leaderboard ID 생성 여부
4. **기존 PlayerProfile 데이터**: 이미 Cloud Save에 저장된 플레이어 데이터가 있는지 (마이그레이션 필요 여부)

---

## [2026-07-09] main 병합 후 재검토 결과

origin/main(사운드 시스템 + 로비 설정/프로필 UI 분리 작업)을 현재 브랜치에 병합한 뒤 계획 영향도를 재확인했다.

### 타겟 파일 충돌 없음 (핵심)
본 작업이 수정할 파일 중 main이 변경한 파일은 **하나도 없다**. 모두 온전하다.
- `ProfileView.cs` — main 미변경
- `RankingView.cs` — main 미변경
- `LoginRootView.cs` / `LoginBootstrapper.cs` / `LoginSelectView.cs` / `SignUpView.cs` — main 미변경

### 문서 충돌 해결
- `GameSystemRules_UI.md` 에서 충돌 발생 → 해결 완료
  - 양쪽 추가분(본 작업: 닉네임/Profile/Ranking 섹션, main: 프로필 서브패널/로비 설정·프로필 UI 섹션)을 모두 보존

### 계획에 영향을 주는 main 변경 사항
1. **로비 탭 구조 변경 (4탭 → 5탭)**: Setting 탭 신규 추가. Profile은 여전히 독립 탭으로 유지 → 본 작업의 Profile 탭 UI 계획과 **정합**함.
2. **Lobby.unity / Login.unity 씬 대폭 변경**: Inspector 작업(ProfileView 필드 생성, RankingPanel 구성, NicknameSetupPanel 생성)은 **새 씬 상태 위에서** 진행해야 함. SettingPanel이 ProfilePanel의 형제로 이미 존재.
3. **Editor 셋업 스크립트 다수 삭제**: `SetupLobbyPanelCanvasGroups.cs`, `AddLogoutButtonToProfileView.cs` 등 1회성 스크립트 삭제됨. CanvasGroup은 이미 씬에 셋업된 상태로 간주.
4. **인게임 설정 메뉴에 "프로필 서브 패널" 추가 (main)**: 이는 **인게임(Game 씬) 설정 메뉴** 안의 프로필 버튼으로, 내부 구성은 "미정" 상태. 본 작업의 **로비(Lobby 씬) Profile 탭 전적 표시**와는 별개 화면이다. 혼동 주의.

### 결론
main 병합으로 인한 **코드 레벨 충돌·재작업은 없음**. 계획은 그대로 유효하며, Inspector 작업만 갱신된 씬 기준으로 진행하면 된다.
