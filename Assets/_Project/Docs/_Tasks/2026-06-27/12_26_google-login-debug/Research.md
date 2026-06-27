# Research: Google 로그인 실패 디버깅

실제 안드로이드 기기에서 구글 로그인 시도 시 계정 선택 UI가 전혀 나타나지 않고
즉시 `signInStatus=Canceled`가 반환되는 현상을 조사하고 해결한 내역입니다.
총 3가지 문제(① 잘못된 인증 메서드 호출, ② google-services.json SHA-1 부족,
③ 실제 빌드 키스토어 SHA-1 미등록 — 근본 원인)를 찾아 해결하여 로그인 성공까지 도달했으며,
이후 단계인 UGS OIDC 브릿지 실패는 별도 이슈로 남아 있습니다.

---

## 1. 증상

### 로그캣 출력 (실기 기기)
```
Warn  Unity  *** [Play Games Plugin 2.1.0] ERROR: Returning an error code.
Info  Unity  GPGS Authenticate 콜백 진입 | signInStatus=Canceled
Error Unity  [FirebaseAuth] GPGS 인증 실패: Canceled
Info  Unity  serverAuthCode 획득 | isNull=False, length=0
```

- 로그인 버튼 터치 → 계정 선택 팝업 **전혀 표시되지 않음**
- `signIn()` 호출 후 약 **34ms 만에 Canceled 반환** (네트워크 요청 없이 로컬 즉시 실패)

---

## 2. 1차 원인 — `Authenticate()` vs `ManuallyAuthenticate()` (수정 완료)

### 원인
`FirebaseAuthService.cs`에서 `PlayGamesPlatform.Instance.Authenticate()`를 호출하고 있었음.

`Authenticate()`의 내부 동작 (GPGS Plugin 2.1.0 `AndroidClient.cs` 실제 코드 기준):

```csharp
// Authenticate(): isAuthenticated() 호출 — 이미 로그인된 세션인지만 확인
public void Authenticate(Action<SignInStatus> callback) { Authenticate(true, callback); }

// ManuallyAuthenticate(): signIn() 호출 — 실제 로그인 UI 표시
public void ManuallyAuthenticate(Action<SignInStatus> callback) { Authenticate(false, callback); }

private void Authenticate(bool isAutoSignIn, Action<SignInStatus> callback)
{
    // isAutoSignIn=true → "isAuthenticated" 메서드 호출
    // isAutoSignIn=false → "signIn" 메서드 호출
    string methodName = isAutoSignIn ? "isAuthenticated" : "signIn";
    using (var client = getGamesSignInClient())
    using (var task = client.Call<AndroidJavaObject>(methodName))
    {
        AndroidTaskUtils.AddOnSuccessListener<AndroidJavaObject>(task, authenticationResult =>
        {
            bool isAuthenticated = authenticationResult.Call<bool>("isAuthenticated");
            SignInOnResult(isAuthenticated, callback);
        });
    }
}

private void SignInOnResult(bool isAuthenticated, Action<SignInStatus> callback)
{
    if (!isAuthenticated)
    {
        OurUtils.Logger.e("Returning an error code."); // ← 로그캣에서 이 로그가 출력됨
        InvokeCallbackOnGameThread(callback, SignInStatus.Canceled);
    }
}
```

`Authenticate()`는 `isAuthenticated()`만 호출 → 기존에 로그인된 세션이 없으면 무조건 Canceled 반환.  
최초 로그인에서는 반드시 `ManuallyAuthenticate()`를 사용해야 `signIn()`이 호출되어 계정 선택 UI가 표시됨.

### 수정 내용

**파일**: `Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs`

```csharp
// 변경 전
PlayGamesPlatform.Instance.Authenticate(signInStatus =>

// 변경 후
// ManuallyAuthenticate: signIn()을 호출하여 실제 로그인 UI를 표시한다.
// Authenticate()는 isAuthenticated()만 호출하므로 최초 로그인 시 무조건 Canceled를 반환한다.
PlayGamesPlatform.Instance.ManuallyAuthenticate(signInStatus =>
```

**상태**: 수정 완료, push 완료

---

## 3. google-services.json SHA-1 업데이트 (완료)

### 배경
Android OAuth 클라이언트는 SHA-1 인증서 해시와 패키지 이름의 조합으로 앱을 식별한다.
빌드에 사용된 키스토어의 SHA-1이 Firebase / Google Cloud Console에 등록되어 있지 않으면
Google Play Games 인증이 실패할 수 있다.

### 이전 상태
`google-services.json`에 Android OAuth 클라이언트가 **1개**만 등록되어 있었음:
- `d5484adf8e549e2777b8c7b77a9082ec964b3e46` (릴리즈 키스토어 SHA-1)

### 업데이트 후 상태
Firebase 콘솔에서 재다운로드한 `google-services.json`에 Android OAuth 클라이언트 **3개** 등록 확인:

| # | SHA-1 | 용도 |
|---|-------|------|
| 1 | `5a0b7a5bc4d4dfc57f29a8d6462174337e2ed9f7` | 디버그 키스토어 |
| 2 | `d5484adf8e549e2777b8c7b77a9082ec964b3e46` | 릴리즈 키스토어 |
| 3 | `4e427a39f7aee0723d3e96c51a9ba5f14eacd246` | Play App Signing 키 |

Web Client ID (GPGS 인증에 사용): `896888428641-8hl0hiov936ccl7mi7gqkrg0h7flgff3.apps.googleusercontent.com`

**상태**: 업데이트 완료, push 완료

---

## 4. 2차 문제 (해결 완료)

`ManuallyAuthenticate()` 수정 및 `google-services.json` 업데이트 후 재빌드했으나
초기에 **증상 동일**: 계정 선택 UI 없이 34ms 만에 Canceled 반환.

### 확인 완료 항목

| 항목 | 확인 방법 | 상태 |
|------|----------|------|
| `ManuallyAuthenticate()` 코드 수정 | 소스코드 직접 확인 | ✅ 완료 |
| `google-services.json` SHA-1 3개 등록 | 파일 내용 직접 확인 | ✅ 완료 |
| GPGS 게시 상태 | Play Console → Play 게임 서비스 → 게시 탭 ("게시할 변경사항 없음" = 게시됨) | ✅ 게시됨 |
| 테스터 등록 | Play Console → 테스터 목록 확인 | ✅ 등록됨 |
| 기기에 Play 게임 앱 설치 및 로그인 | 실기 확인 | ✅ 확인됨 |

---

## 5. 문제 3 — 실제 빌드 키스토어 SHA-1 미등록 (근본 원인, 해결 완료)

### 진단 방법
Unity 태그 필터를 제거하고 로그인 시도 시점의 전체 로그캣을 캡처한 결과,
`PlayGamesServices[SignInAuthenticator]` 태그에서 **실제 APK 서명에 사용된 SHA-1**을 확인할 수 있었다.

```
실제 APK 서명 SHA-1 (logcat Cert SHA1 fingerprint):
18:E0:32:5F:5A:F9:C5:A7:3F:22:34:BE:65:1F:E6:CA:61:2E:DE:3D
```

### 원인
이 SHA-1은 그동안 등록해 온 3개(`5a0b...` 디버그 / `d548...` 릴리즈 / `4e42...` Play App Signing) 중
**어느 것과도 일치하지 않았다**.

근본 원인은 실제 빌드에 사용한 `hexiege-release.keystore` 파일이
SHA-1을 등록할 때 사용한 키스토어와 **다른 파일**이었다는 점이다.
즉 등록된 SHA-1과 실제 서명 SHA-1이 서로 달라, Google Play Games `signIn()`이
앱을 검증하지 못하고 즉시 Canceled를 반환한 것이다.

### 해결
1. 실제 서명 SHA-1(`18:E0:...:3D`)을 **Firebase Console**에 추가 등록 후 `google-services.json` 재다운로드
2. 동일 SHA-1을 **Play Console → Play 게임 서비스 → 설정 → 사용자 인증 정보**에 추가 등록 및 게시
3. **Firebase Authentication에서 Play Games 제공업체 활성화** (Web Client ID + Web Client Secret 입력)

> SHA-1은 ① Firebase Console(OAuth 클라이언트), ② Play Console GPGS 사용자 인증 정보,
> ③ 실제 빌드에 사용된 키스토어 — **세 곳이 모두 일치**해야 GPGS `signIn()`이 성공한다.

---

## 6. 최종 결과 (해결 확인)

실제 서명 SHA-1 등록 + Firebase Play Games 제공업체 활성화 후 재시도한 결과,
계정 선택 UI가 정상 표시되고 로그인이 성공했다.

```
✅ signInStatus=Success
✅ GPGS Server Auth Code 발급 성공 (length=73)   ← 이전 length=0 → 정상 발급
✅ Google Firebase 로그인 성공 | UID=xdmWpVNyyvaBe0cB878mSg0URm83
✅ IsLoggedIn=True, IsAnonymous=False
```

`serverAuthCode length=0`(3절 미확인 항목 1)은 SHA-1 불일치로 인증이 실패해
빈 값이 반환된 것이었으며, SHA-1 정합 후 `length=73`으로 정상 발급되어 해소되었다.

---

## 7. 잔여 이슈 — UGS OIDC 브릿지 실패 (별도 이슈, 미해결)

Google Firebase 로그인 성공 직후 UGS OIDC 브릿지 단계에서 아래 경고가 출력됨:

```
⚠️ UGS OIDC 브릿지 실패 (id provider not found)
```

- Firebase 로그인 자체는 성공(UID 발급 완료)이나, Firebase ID Token을 UGS OIDC로 연결하는
  `SignInWithOpenIdConnectAsync("oidc-firebase")` 단계에서 UGS Dashboard에 OIDC 제공자(`oidc-firebase`)가
  등록되지 않아 실패.
- **영향**: UGS PlayerId 미발급 → 멀티플레이(Lobby/Relay) 기능 제한.
- **이번 작업 범위 밖**의 별도 이슈로, UGS Dashboard OIDC Provider 등록 후 재확인 필요.

---

## 8. 관련 파일

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs` | `ManuallyAuthenticate()` 수정 완료 |
| `Assets/google-services.json` | 실제 빌드 키스토어 SHA-1(`18:E0:...:3D`) 추가 후 재다운로드 완료 |
| `Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` | 빌드 시 google-services.json에서 자동 생성됨. `default_android_client_id`에 디버그 SHA-1 클라이언트 ID 저장 (GPGS signIn()에는 직접 영향 없음) |
| `ProjectSettings/ProjectSettings.asset` | 릴리즈 키스토어 경로 설정 포함 |
| `Assets/GooglePlayGames/com.google.play.games/Runtime/Scripts/Platforms/Android/AndroidClient.cs` | GPGS Plugin 내부 코드 — `Authenticate()` vs `ManuallyAuthenticate()` 동작 차이 확인에 사용 |
| `ProjectSettings/GooglePlayGameSettings.txt` | App ID: 896888428641, ClientId: Web Client ID |
