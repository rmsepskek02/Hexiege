# Research: Google 로그인 실패 디버깅

실제 안드로이드 기기에서 구글 로그인 시도 시 계정 선택 UI가 전혀 나타나지 않고
즉시 `signInStatus=Canceled`가 반환되는 현상을 조사한 내역입니다.
어떤 부분이 확인되었고, 어떤 부분이 아직 미해결인지를 정리합니다.

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

## 4. 현재 상태 — 2차 문제 (미해결)

`ManuallyAuthenticate()` 수정 및 `google-services.json` 업데이트 후 재빌드했으나
**증상 동일**: 계정 선택 UI 없이 34ms 만에 Canceled 반환.

### 확인 완료 항목

| 항목 | 확인 방법 | 상태 |
|------|----------|------|
| `ManuallyAuthenticate()` 코드 수정 | 소스코드 직접 확인 | ✅ 완료 |
| `google-services.json` SHA-1 3개 등록 | 파일 내용 직접 확인 | ✅ 완료 |
| GPGS 게시 상태 | Play Console → Play 게임 서비스 → 게시 탭 ("게시할 변경사항 없음" = 게시됨) | ✅ 게시됨 |
| 테스터 등록 | Play Console → 테스터 목록 확인 | ✅ 등록됨 |
| 기기에 Play 게임 앱 설치 및 로그인 | 실기 확인 | ✅ 확인됨 |

### 미확인 항목 1 — `serverAuthCode length=0` 의미 불명확

로그인 실패 시 아래 로그가 함께 출력됨:
```
Info Unity  serverAuthCode 획득 | isNull=False, length=0
```
- `isNull=False` → serverAuthCode 변수 자체는 null이 아님 (값이 존재)
- `length=0` → 그러나 실제 내용은 빈 문자열

로그인이 실패했음에도 serverAuthCode가 "빈 값으로" 남아 있다는 것이 정상 동작인지,
아니면 실패 원인과 관련된 단서인지 **아직 분석되지 않음**.

---

### 미확인 항목 2 — Play Console GPGS 사용자 인증 정보 SHA-1 등록 여부

SHA-1을 등록해야 하는 위치는 두 군데이며 **서로 독립적**이다:

| 위치 | 용도 | 확인 여부 |
|------|------|----------|
| Firebase 콘솔 (Google Cloud Console) → OAuth 2.0 클라이언트 | Firebase 인증 / google-services.json 생성 | ✅ 3개 확인됨 |
| Play Console → Play 게임 서비스 → 설정 → 사용자 인증 정보 | GPGS signIn() 인증 | ❓ 미확인 |

GPGS `signIn()`은 Play Console의 GPGS 사용자 인증 정보를 기준으로 앱을 검증한다.
Firebase 콘솔에 SHA-1이 등록되어 있어도 Play Console GPGS 쪽에 등록되지 않으면
`signIn()`이 실패할 수 있다. **이 항목이 현재 2차 문제의 유력 원인 후보.**

---

### 아직 불명확한 부분 (근본 원인)

`signIn()`이 34ms 만에 실패하는 원인이 **Unity 로그만으로는 확인 불가**.  
Google Play Services 네이티브 레이어(`com.google.android.gms.*`)에서 어떤 에러가 발생하는지
Unity 필터를 제거한 전체 로그캣을 확인해야 한다.

**다음 진단 단계 (우선순위 순)**:
1. Play Console → Play 게임 서비스 → 설정 → 사용자 인증 정보에 SHA-1 3개가 모두 등록되어 있는지 확인
2. Logcat에서 Unity 태그 필터 제거 → 로그인 시도 시점의 `com.google.android.gms` 또는 `PlayGames` 태그 로그 캡처

---

## 5. 관련 파일

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs` | `ManuallyAuthenticate()` 수정 완료 |
| `Assets/google-services.json` | SHA-1 3개로 업데이트 완료 |
| `Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` | 빌드 시 google-services.json에서 자동 생성됨. `default_android_client_id`에 디버그 SHA-1 클라이언트 ID 저장 (GPGS signIn()에는 직접 영향 없음) |
| `ProjectSettings/ProjectSettings.asset` | 릴리즈 키스토어 경로 설정 포함 |
| `Assets/GooglePlayGames/com.google.play.games/Runtime/Scripts/Platforms/Android/AndroidClient.cs` | GPGS Plugin 내부 코드 — `Authenticate()` vs `ManuallyAuthenticate()` 동작 차이 확인에 사용 |
| `ProjectSettings/GooglePlayGameSettings.txt` | App ID: 896888428641, ClientId: Web Client ID |
