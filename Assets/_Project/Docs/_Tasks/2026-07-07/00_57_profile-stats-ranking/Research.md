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
- `LobbyTab.Ranking` 탭 열거값 이미 정의됨
- UniRx `ReactiveProperty`, `Subject` 패턴 사용

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
