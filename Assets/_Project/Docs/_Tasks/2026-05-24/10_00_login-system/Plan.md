# Plan: 로그인 시스템 구현 (Firebase Auth)

이 문서는 로그인 시스템을 실제로 만들어 나가는 순서와 방법을 담고 있습니다. 현재 Hexiege는 로그인 화면 없이 바로 로비로 진입하는 구조인데, 이번 작업에서는 Firebase Authentication을 기반으로 한 별도의 로그인 씬(Login.unity)을 만들고, 익명/Google/이메일 세 가지 방식의 로그인을 지원합니다. 기존 멀티플레이 코드(LobbyManager, RelayManager 등)는 변경하지 않고, 최소한의 수정으로 연결합니다.

---

## 규칙 문서 참조

| 문서 | 적용 여부 | 비고 |
|------|---------|------|
| `GameSystemRules.md` | 적용 없음 | 로그인 시스템은 게임 메커니즘과 무관 |
| `AuthSystemRules.md` | **필수 참조** | 로그인/인증 관련 모든 규칙의 단일 권위 소스 |

구현 에이전트는 반드시 `AuthSystemRules.md`를 먼저 읽은 후 구현을 시작해야 합니다.

---

## 사전 조건 (코드 작성 전에 완료해야 함)

구현 에이전트에게 위임하기 전, **사용자가 직접 수행해야 하는 환경 설정**입니다.
상세 절차: `ThirdPartySetup.md` 참조

| 단계 | 항목 | 완료 여부 |
|------|------|---------|
| 1 | Firebase Console — 프로젝트 생성 및 Android 앱 등록 | ☐ (미완료 — 추후 진행) |
| 2 | Firebase Authentication — 3가지 로그인 방법 활성화 | ☐ (미완료 — 추후 진행) |
| 3 | `google-services.json` → `Assets/` 배치 | ☐ (미완료 — 추후 진행) |
| 4 | Firebase Unity SDK v13.11.0 `FirebaseAuth.unitypackage` Unity 임포트 | ✅ 완료 (2026-05-24) |
| 5 | Google Play Games Plugin v2.1.0 임포트 | ✅ 완료 (2026-05-24, GitHub `current-build/` 폴더 내 .unitypackage) |
| 5-2 | GPGS 웹 클라이언트 ID 설정 (`Window > Google Play Games > Setup`) | ☐ (미완료 — [1][2] 이후 진행) |
| 6 | mainTemplate.gradle `multiDexEnabled true` 설정 | ✖ 불필요 (Min API Level = 25, API 21+ 내장 Multidex) |

---

## 구현 단계

### [1] Login.unity 씬 생성

- 경로: `Assets/_Project/Scenes/Login.unity`
- 빈 씬 생성 후 Canvas (Screen Space - Overlay) 추가
- Build Settings에 등록
- 로그인 기능 테스트 시: Login = 0, Lobby = 1, Game = 2

---

### [2] FirebaseAuthService.cs 구현

**경로**: `Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs`

Firebase SDK를 감싸는 래퍼 클래스. 상위 레이어가 Firebase SDK에 직접 의존하지 않도록 분리합니다.

**제공해야 하는 API**:
```
bool IsLoggedIn                          // 현재 로그인 상태 확인
bool IsAnonymous                         // 익명 로그인 여부
string FirebaseUID                       // 현재 사용자 UID
string DisplayName                       // 표시 이름 (Google 계정)
string Email                             // 이메일

Task<string> SignInAnonymouslyAsync()    // 익명 로그인 → Firebase UID 반환
Task<string> SignInWithGoogleAsync()     // Google 로그인 (GPGS idToken → Firebase)
Task<string> SignInWithEmailAsync(email, password)    // 이메일 로그인
Task SignUpWithEmailAsync(email, password, displayName)  // 이메일 회원가입
Task SendEmailVerificationAsync()        // 인증 메일 발송
Task<bool> CheckEmailVerifiedAsync()     // 인증 완료 여부 확인 (Firebase 서버에 재쿼리)
Task SendPasswordResetEmailAsync(email)  // 비밀번호 재설정 메일
Task LinkWithGoogleAsync()               // 익명 → Google 계정 연동
Task LinkWithEmailAsync(email, password) // 익명 → 이메일 계정 연동
Task SignOutAsync()                      // 로그아웃
```

**Google 로그인 흐름** (AuthSystemRules.md 규칙):
```
PlayGamesPlatform.Instance.Authenticate()
  → 성공 시 PlayGamesPlatform.Instance.RequestServerSideAccess(forceRefresh: true)
  → serverAuthCode 획득
  → Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode)
  → FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential)
  → Firebase UID 반환
```

**에러 처리**: `FirebaseException` 캐치 → `AuthException` (도메인 예외)으로 변환하여 반환

---

### [3] LoginBootstrapper.cs 구현

**경로**: `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs`

Login.unity 씬 전용 Composition Root. `GameBootstrapper`와 완전히 독립적으로 동작합니다.

**역할**:
1. Firebase SDK 초기화 (`FirebaseApp.CheckAndFixDependenciesAsync()`)
2. 세션 확인: `FirebaseAuth.DefaultInstance.CurrentUser`가 존재하면 → UGS 브릿지 → Lobby 씬 이동 (자동 로그인)
3. 세션 없으면 → `LoginRootView` 활성화하여 로그인 선택 화면 표시
4. 의존성 주입: `FirebaseAuthService`, `LoginUseCase`, `AccountLinkUseCase` 인스턴스 생성 및 View에 주입

**자동 로그인 조건** (AuthSystemRules.md):
- `FirebaseAuth.DefaultInstance.CurrentUser != null`
- 익명 로그인 상태도 자동 로그인 처리 (세션 유지)
- 자동 로그인 성공 → `LoginUseCase.BridgeToUGSAsync()` 호출 → `SceneManager.LoadScene("Lobby")`

---

### [4] LoginUseCase.cs 구현

**경로**: `Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs`

로그인 흐름을 조율하는 Application 레이어 클래스. Firebase SDK에 직접 의존하지 않습니다.

**제공해야 하는 메서드**:
```
Task<LoginResult> SignInAnonymouslyAsync()
Task<LoginResult> SignInWithGoogleAsync()
Task<LoginResult> SignInWithEmailAsync(email, password)
Task<LoginResult> SignUpWithEmailAsync(email, password, displayName)
Task BridgeToUGSAsync(string firebaseUID)   // Firebase UID → UGS SignInWithCustomIdAsync
Task SendEmailVerificationAsync()
Task<bool> CheckEmailVerifiedAsync()
Task SendPasswordResetEmailAsync(email)
```

**UGS 브릿지 구현** (`BridgeToUGSAsync`):
```csharp
await UnityServices.InitializeAsync();
await AuthenticationService.Instance.SignInWithCustomIdAsync(firebaseUID, createAccount: true);
```

이 메서드는 `UnityServicesInitializer.cs` 수정과 연계됩니다.

**LoginResult 구조**:
```csharp
public enum LoginResult { Success, NeedsEmailVerification, Failed }
```

---

### [5] AccountLinkUseCase.cs 구현

**경로**: `Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs`

익명 계정을 실제 계정으로 연동하는 흐름 조율 클래스.

**제공해야 하는 메서드**:
```
Task LinkWithGoogleAsync()
Task LinkWithEmailAsync(email, password)
```

**연동 성공 처리**: Firebase UID는 동일하게 유지되므로 UGS 재연결 불필요.
**연동 실패 (이미 사용 중인 계정)**: `AuthException.CredentialAlreadyInUse` → 사용자에게 안내 팝업.

---

### [6] 7개 View 파일 구현

모든 View는 `MonoBehaviour`이며 `LoginBootstrapper`에서 참조를 주입받습니다.

#### LoginRootView.cs
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`

화면 전환 조율 및 패널 스택 관리.

```
ShowLoginSelect()       // 로그인 선택 화면 표시
ShowEmailLogin()        // 이메일 로그인 화면 표시
ShowSignUp()            // 회원가입 화면 표시
ShowEmailVerify()       // 이메일 인증 대기 화면 표시
ShowPasswordReset()     // 비밀번호 재설정 화면 표시
ShowAnonymousWarning()  // 익명 경고 팝업 표시
HideAll()               // 모든 패널 숨기기
```

Back 버튼 동작: 패널 스택 기반으로 이전 화면으로 이동. 첫 화면(LoginSelectView)에서 Back → 앱 종료 확인 팝업.

#### LoginSelectView.cs
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginSelectView.cs`

- Google 로그인 버튼 → `LoginUseCase.SignInWithGoogleAsync()` 호출
- 이메일 로그인 버튼 → `LoginRootView.ShowEmailLogin()`
- 익명으로 시작하기 버튼 → `LoginRootView.ShowAnonymousWarning()`
- 로딩 중 UI 비활성화 처리

#### EmailLoginView.cs
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/EmailLoginView.cs`

- 이메일 / 비밀번호 InputField
- 로그인 버튼 → `LoginUseCase.SignInWithEmailAsync()`
- 회원가입 버튼 → `LoginRootView.ShowSignUp()`
- 비밀번호 찾기 버튼 → `LoginRootView.ShowPasswordReset()`
- 뒤로가기 버튼 → `LoginRootView.ShowLoginSelect()`

#### SignUpView.cs
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/SignUpView.cs`

- 이메일 / 비밀번호 / 비밀번호 확인 InputField
- 회원가입 버튼 → `LoginUseCase.SignUpWithEmailAsync()` → 성공 시 `LoginUseCase.SendEmailVerificationAsync()` → `LoginRootView.ShowEmailVerify()`
- 뒤로가기 버튼

#### EmailVerifyView.cs
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/EmailVerifyView.cs`

- "인증 완료" 버튼 → `LoginUseCase.CheckEmailVerifiedAsync()` → true면 UGS 브릿지 → Lobby 씬 이동
- "재발송" 버튼 → `LoginUseCase.SendEmailVerificationAsync()`
- 인증 완료 전에는 Lobby 이동 불가

#### PasswordResetView.cs
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/PasswordResetView.cs`

- 이메일 InputField
- "재설정 메일 발송" 버튼 → `LoginUseCase.SendPasswordResetEmailAsync()` → 성공 메시지 표시 → `LoginRootView.ShowEmailLogin()`
- 뒤로가기 버튼

#### AnonymousWarningPopup.cs
**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs`

오버레이 팝업. 기존 `ConfirmPopup.cs` 패턴 참고.

- "계정 만들기" 버튼 → `LoginRootView.ShowSignUp()`
- "익명으로 계속" 버튼 → `LoginUseCase.SignInAnonymouslyAsync()` → UGS 브릿지 → Lobby 씬 이동

---

### [7] UnityServicesInitializer.cs 수정 ✅ 완료 (2026-05-24)

**경로**: `Assets/_Project/Scripts/Infrastructure/Network/UnityServicesInitializer.cs`

**당초 계획**: 기존 `SignInAnonymouslyAsync()` 완전 제거 → `SignInWithCustomIdAsync(firebaseUID)` 방식으로 교체.

**실제 구현**: 항상 재로그인 방식으로 변경.

**변경 이유**:
1. 설치된 UGS Authentication SDK가 `SignInWithCustomIdAsync` 메서드를 지원하지 않음. Firebase UID → UGS Custom ID 브릿지는 추후 UGS SDK 업데이트 시 구현 예정.
2. (2026-05-24 추가) `IsSignedIn=true`(기기 캐시)이지만 서버 토큰이 만료된 상태에서 재로그인을 건너뛰면 UGS Lobby API 호출 시 HTTP 401 Unauthorized 에러 발생 → 커스텀 게임 / 랜덤 매칭 실패. 이를 수정하기 위해 `IsSignedIn` 체크를 제거하고 항상 재로그인하도록 변경.

**현재 적용된 변경**:
```csharp
// 항상 재로그인하여 서버로부터 유효한 토큰을 발급받는다.
if (AuthenticationService.Instance.IsSignedIn)
{
    AuthenticationService.Instance.SignOut();
}
await AuthenticationService.Instance.SignInAnonymouslyAsync();
```

**효과**:
- Lobby 직접 실행 (게임 테스트) + Login 씬 경유 모두: 매 초기화 시 신선한 UGS 토큰 보장 → 멀티플레이 정상 동작 확인

> ⚠️ **추후 재검토 필요**: Firebase UID → UGS 브릿지(`SignInWithCustomIdAsync`)가 정상 구현되면, Login 씬이 발급한 Firebase UID 기반 UGS 세션을 Lobby 씬 진입 시 `InitializeAsync()`가 덮어쓰는 문제가 발생한다. 이 시점에 `InitializeAsync()` 재인증 로직 전체를 재설계해야 한다. (토큰 만료 문제도 함께 고려)

> 참고: `LobbyManager.cs`는 `AuthenticationService.Instance.PlayerId`만 사용하므로 추가 수정 불필요.

---

### [8] ProfileView.cs 구현

**경로**: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs`

현재 빈 파일 → 계정 상태에 따라 다른 UI 표시.

**익명 상태**:
- "익명 사용자" 표시
- "계정 연동" 버튼 → Google 연동 / 이메일 연동 선택지
- "로그아웃" 버튼 (익명 로그아웃 → Login 씬 이동)

**실계정 상태 (Google 또는 이메일)**:
- 표시 이름 / 이메일 표시
- "로그아웃" 버튼 → `FirebaseAuthService.SignOutAsync()` → `SceneManager.LoadScene("Login")`

**계정 연동 흐름**: `AccountLinkUseCase` 호출 → 성공 시 UI 갱신.

---

## 파일 변경 요약

| 파일 | 상태 | 구현 여부 | 주요 내용 |
|------|------|---------|---------|
| `Login.unity` | 신규 | ❌ 미완료 | 로그인 씬 (Inspector 연결 포함, Firebase Console 설정 이후 진행) |
| `FirebaseAuthService.cs` | 신규 | ✅ 완료 | Firebase SDK 래퍼. SignInWithCredentialAsync 반환값 FirebaseUser로 처리 (SDK 13.x) |
| `LoginBootstrapper.cs` | 신규 | ✅ 완료 | Login 씬 Composition Root |
| `LoginUseCase.cs` | 신규 | ✅ 완료 | 로그인 흐름 조율 + UGS 브릿지 (현재 익명 임시, SignInWithCustomIdAsync 추후) |
| `AccountLinkUseCase.cs` | 신규 | ✅ 완료 | 계정 연동 흐름 |
| `LoginRootView.cs` | 신규 | ✅ 완료 | 화면 전환 조율. UnityEngine.Application.Quit() 명시 (네임스페이스 충돌 방지) |
| `LoginSelectView.cs` | 신규 | ✅ 완료 | 로그인 방식 선택 화면 |
| `EmailLoginView.cs` | 신규 | ✅ 완료 | 이메일 로그인 화면 |
| `SignUpView.cs` | 신규 | ✅ 완료 | 이메일 회원가입 화면 |
| `EmailVerifyView.cs` | 신규 | ✅ 완료 | 이메일 인증 대기 화면 |
| `PasswordResetView.cs` | 신규 | ✅ 완료 | 비밀번호 재설정 화면 |
| `AnonymousWarningPopup.cs` | 신규 | ✅ 완료 | 익명 경고 팝업 |
| `UnityServicesInitializer.cs` | **수정** | ✅ 완료 | 항상 SignOut() → SignInAnonymouslyAsync() 수행 (UGS 401 버그 수정). ⚠️ Firebase UID → UGS 브릿지 구현 시 재검토 필요 |
| `ProfileView.cs` | **수정** | ✅ 완료 | 빈 파일 → 계정 연동/로그아웃 UI 구현 |

---

## 리스크 및 주의사항

| 리스크 | 내용 | 대응 |
|--------|------|------|
| Firebase SDK 빌드 오류 | Multidex 관련 오류 | Min API Level 25 기준 불필요. 발생 시 `ThirdPartySetup.md` [3] 참조 |
| GPGS v2 패키지 형식 | v2.1.0부터 GitHub Releases에 .unitypackage가 없음 | `current-build/` 폴더 내 .unitypackage 사용 (v1 deprecated 2026-05) |
| UGS 브릿지 미구현 | 현재 UGS SDK에 `SignInWithCustomIdAsync` 없음 | 임시로 익명 로그인 사용 (LoginUseCase.cs TODO 주석). 추후 UGS SDK 업데이트 시 교체 |
| 이메일 인증 타이밍 | `CheckEmailVerifiedAsync()` 호출 시 Firebase 서버에 재쿼리 필요 | `FirebaseAuth.CurrentUser.ReloadAsync()` 후 `IsEmailVerified` 확인 |
| GPGS 초기화 순서 | `PlayGamesPlatform.Activate()` 는 게임 시작 시 단 한 번만 호출 | `LoginBootstrapper.Awake()`에서 처리 (현재 구현 완료) |
| Firebase Console 미설정 | google-services.json 없으면 런타임 오류 | 코드는 컴파일되나 실제 로그인 기능 비활성. Firebase Console 설정 이후 Login.unity 씬 구성 진행 |

---

## 에이전트 위임 계획

구현은 **game-programmer** 에이전트에게 위임합니다.

에이전트에게 전달해야 하는 문서:
1. `AuthSystemRules.md` — 로그인 규칙 전체 (단일 권위 소스)
2. `Research.md` — 현재 구조 파악, 영향 파일 목록
3. `Plan.md` (이 문서) — 구현 순서 및 파일별 상세 명세
4. `UIWireframe.md` — 화면별 UI 구조 및 Inspector 컴포넌트 목록
5. `ThirdPartySetup.md` — SDK 설치 완료 여부 확인용

**위임 전 사전 조건**: 사용자가 `ThirdPartySetup.md` 가이드에 따라 Firebase Console 및 SDK 설치를 완료한 후 구현 시작.

---

## 구현 순서 요약

```
[사전-1] SDK 설치 (2026-05-24 ✅)
  - Firebase Unity SDK v13.11.0 (FirebaseAuth.unitypackage)
  - Google Play Games Plugin v2.1.0 (current-build/.unitypackage)
  - EDM4U 의존성 해결 (Custom Main/Gradle Properties Template 활성화)

[사전-2] Firebase Console 설정 (미완료 — 추후 진행)
  - 프로젝트 생성 + Android 앱 등록 (SHA-1 포함)
  - Authentication 방식 3종 활성화
  - google-services.json → Assets/ 배치

[1] Login.unity 씬 생성 (미완료 — 사전-2 이후 진행)
[2] FirebaseAuthService.cs ✅ 완료
[3] LoginBootstrapper.cs ✅ 완료
[4] LoginUseCase.cs ✅ 완료 (UGS 브릿지는 임시 익명 로그인)
[5] AccountLinkUseCase.cs ✅ 완료
[6-1] LoginRootView.cs ✅ 완료
[6-2] LoginSelectView.cs ✅ 완료
[6-3] EmailLoginView.cs ✅ 완료
[6-4] SignUpView.cs ✅ 완료
[6-5] EmailVerifyView.cs ✅ 완료
[6-6] PasswordResetView.cs ✅ 완료
[6-7] AnonymousWarningPopup.cs ✅ 완료
[7] UnityServicesInitializer.cs 수정 ✅ 완료 (폴백 익명 로그인)
[8] ProfileView.cs 구현 ✅ 완료
  ↓
[후-1] Inspector 연결 (Login.unity 씬 구성 — 사전-2 이후 진행)
[후-2] GPGS 웹 클라이언트 ID 설정 (사전-2 이후 진행)
[후-3] Firebase UID → UGS Custom ID 브릿지 구현 (UGS SDK 업데이트 대기)
```
