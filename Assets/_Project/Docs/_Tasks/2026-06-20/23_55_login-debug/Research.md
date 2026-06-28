# Research: 로그인 동작 안 함 — 원인 파악을 위한 RuntimeLog 추가

## 작업 목적

실기기(Android)에서 로그인 버튼을 눌렀을 때:
- **익명 로그인**: 아무 반응이 없음
- **Google 로그인**: "실패했다"는 문구만 표시되고 원인을 알 수 없음

두 증상의 원인을 정확히 파악하기 위해 RuntimeLog를 주요 흐름에 추가한다.
Claude가 로그 파일을 읽고 어느 단계에서 무슨 에러가 발생했는지 추적할 수 있도록 하는 것이 목적이다.

---

## 현재 로그인 흐름 구조

```
LoginBootstrapper.Start()
  └─ InitializeAndDispatchAsync()
       ├─ FirebaseAuthService.InitializeAsync()   ← Firebase SDK 초기화
       ├─ LoginUseCase 생성
       ├─ 의존성 주입 (InjectDependencies)
       └─ 자동 로그인 or 로그인 선택 화면 표시

[사용자가 익명 버튼 클릭]
  LoginSelectView.OnAnonymousClicked()
    └─ LoginRootView.ShowAnonymousWarning()       ← 팝업 표시 (즉시 로그인 X)
         └─ AnonymousWarningPopup (확인 클릭 시)
              └─ LoginUseCase.SignInAnonymouslyAsync()
                   └─ FirebaseAuthService.SignInAnonymouslyAsync()
                        └─ Firebase SDK: _auth.SignInAnonymouslyAsync()

[사용자가 Google 버튼 클릭]
  LoginSelectView.OnGoogleLoginClicked()
    └─ LoginUseCase.SignInWithGoogleAsync()
         └─ FirebaseAuthService.SignInWithGoogleAsync()
              ├─ RequestGoogleServerAuthCodeAsync()
              │    ├─ PlayGamesPlatform.Instance.Authenticate(callback)  ← GPGS 인증
              │    └─ PlayGamesPlatform.Instance.RequestServerSideAccess(callback)  ← Auth Code 발급
              └─ _auth.SignInWithCredentialAsync(credential)  ← Firebase 로그인
```

---

## 증상별 추정 원인

### 익명 로그인 — 아무 반응 없음

익명 버튼은 **즉시 로그인하지 않고 AnonymousWarningPopup을 먼저 표시**한다 (AuthSystemRules.md 규칙).
실제 로그인은 팝업에서 "확인"을 눌러야 시작된다.

가능한 원인:
1. `AnonymousWarningPopup`이 씬에서 Inspector에 연결되지 않아 팝업 자체가 뜨지 않음
2. `AnonymousWarningPopup.Initialize()`가 호출되지 않아 버튼 리스너가 등록되지 않음
3. Firebase 초기화 실패 → 이후 로그인 시도 시 `EnsureInitialized()` 예외 발생 → catch 되어 `LoginResult.Failed` 반환되지만 `LastError`가 View에 표시되지 않음

### Google 로그인 — 실패 문구 표시

`AuthException`이 throw되어 `LoginResult.Failed`로 반환된 것은 확인됨.
실패 지점이 세 곳 중 어디인지 알 수 없음:
1. `PlayGamesPlatform.Authenticate` 실패 (GPGS 인증 자체 실패)
2. `RequestServerSideAccess` 실패 (Auth Code 발급 실패)
3. `_auth.SignInWithCredentialAsync` 실패 (Firebase 로그인 실패)

가능한 원인:
- Play Console 게임 서비스가 아직 "임시" 상태 — 앱이 게시되지 않아 GPGS 인증 실패 가능
- SHA-1 인증서 불일치 — Firebase에 등록된 SHA-1과 빌드 서명 불일치
- OAuth 동의 화면 미완성 — 테스터 미등록 상태

---

## RuntimeLog 추가 대상 위치

| 파일 | 위치 | 로그 목적 |
|------|------|----------|
| `FirebaseAuthService.cs` | `InitializeAsync()` 성공/실패 | Firebase 초기화 상태 확인 |
| `FirebaseAuthService.cs` | `SignInAnonymouslyAsync()` 진입/성공/실패 | 익명 로그인 흐름 추적 |
| `FirebaseAuthService.cs` | `RequestGoogleServerAuthCodeAsync()` — Authenticate 콜백 | GPGS 인증 실패 상태 확인 |
| `FirebaseAuthService.cs` | `RequestGoogleServerAuthCodeAsync()` — RequestServerSideAccess 콜백 | Auth Code 발급 실패 확인 |
| `FirebaseAuthService.cs` | `SignInWithGoogleAsync()` Firebase 로그인 성공/실패 | Firebase 자격증명 로그인 결과 |
| `AnonymousWarningPopup.cs` | `Initialize()` 및 확인 버튼 콜백 | 팝업 연결 및 확인 클릭 여부 확인 |
| `LoginBootstrapper.cs` | `InitializeAndDispatchAsync()` Firebase 초기화 결과 | 초기화 실패 시 후속 흐름 확인 |

---

## 관련 파일 목록

| 파일 | 경로 |
|------|------|
| FirebaseAuthService.cs | `Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs` |
| LoginUseCase.cs | `Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs` |
| LoginBootstrapper.cs | `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs` |
| LoginSelectView.cs | `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginSelectView.cs` |
| AnonymousWarningPopup.cs | `Assets/_Project/Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs` |

---

## 로그 파일 출력 위치

```
Assets/_Project/Docs/_Logs/2026-06-20/23_55_login-debug/RuntimeLog_client.txt
```
