# Plan: 로그인 동작 안 함 — RuntimeLog 추가

## 작업 목적

실기기에서 익명 로그인 무반응, Google 로그인 실패 증상의 원인을 파악하기 위해
주요 로그인 흐름에 RuntimeLog(파일 출력)를 추가한다.
로그 파일을 Claude가 읽고 어느 단계에서 어떤 에러가 발생했는지 추적한다.

---

## 구현 방식

### RuntimeLog 출력 구조

`LogRules.md` 형식을 따른다:
```
[HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value, key=value
```

파일 헬퍼 유틸리티를 별도 클래스로 만들지 않고,
각 파일에서 `System.IO.File.AppendAllText`를 직접 호출하는 방식으로 추가한다.
(디버깅 전용 임시 코드 — 작업 완료 후 제거)

로그 파일 경로:
```
Assets/_Project/Docs/_Logs/2026-06-20/23_55_login-debug/RuntimeLog_client.txt
```

> Android 기기에서는 `Application.persistentDataPath` 기준으로 경로를 설정해야 실기기에서 파일이 생성된다.
> 실제 경로: `{Application.persistentDataPath}/RuntimeLog_client.txt`
> 빌드 후 파일은 `adb pull` 로 추출하거나 Android Logcat 대신 활용한다.

---

## 수정 파일 및 변경 내용

### 1. `FirebaseAuthService.cs`

**추가 위치 1 — `InitializeAsync()`**
- 초기화 성공/실패 모두 RuntimeLog 출력
- `DependencyStatus` 값도 함께 기록

**추가 위치 2 — `SignInAnonymouslyAsync()`**
- 메서드 진입 시 `[INFO]` 로그
- 성공 시 UID 포함 `[INFO]` 로그
- `FirebaseException` catch 시 에러 코드 포함 `[ERROR]` 로그

**추가 위치 3 — `RequestGoogleServerAuthCodeAsync()`**
- `Authenticate` 콜백: 성공/실패 상태 `[INFO]` / `[ERROR]`
- `RequestServerSideAccess` 콜백: Auth Code 발급 성공/실패 `[INFO]` / `[ERROR]`

**추가 위치 4 — `SignInWithGoogleAsync()`**
- Firebase `SignInWithCredentialAsync` 성공/실패 `[INFO]` / `[ERROR]`

### 2. `AnonymousWarningPopup.cs`

**추가 위치 1 — `Initialize()`**
- 메서드 진입 시 `[INFO]` — 팝업이 초기화됐는지 확인용

**추가 위치 2 — `Show()`**
- 팝업이 실제로 표시되는지 확인용 `[INFO]`

**추가 위치 3 — `OnContinueAnonymousClicked()`**
- 버튼 클릭 진입 `[INFO]` — 버튼 리스너가 등록됐는지 확인용
- `LoginResult` 값 `[INFO]` / `[ERROR]`

### 3. `LoginBootstrapper.cs`

**추가 위치 1 — `InitializeAndDispatchAsync()` 헤더**
- 세션 시작 헤더 출력 (LogRules.md 형식)

**추가 위치 2 — Firebase 초기화 결과**
- `fbReady` 값 및 분기 흐름 `[INFO]` / `[ERROR]`

---

## 로그 파일 추출 방법 (빌드 후)

Android 기기에서 앱 실행 후:
```
adb shell "run-as com.gorocompany.hexiege cat /data/data/com.gorocompany.hexiege/files/RuntimeLog_client.txt"
```
또는:
```
adb pull /sdcard/Android/data/com.gorocompany.hexiege/files/RuntimeLog_client.txt
```

---

## 작업 완료 기준

1. 빌드 후 실기기에서 익명/Google 로그인 시도
2. 로그 파일 추출
3. 로그 파일을 Claude에게 전달 → 원인 분석

---

## 작업 완료 후 제거 대상

RuntimeLog 출력 코드는 디버깅 전용이므로 원인 파악 후 반드시 제거:
- `FirebaseAuthService.cs` 내 `RuntimeLog.*` 호출 전체
- `AnonymousWarningPopup.cs` 내 `RuntimeLog.*` 호출 전체
- `LoginBootstrapper.cs` 내 `RuntimeLog.*` 호출 전체

로그 **파일** 자체(`RuntimeLog_client.txt`)는 `_Logs/` 폴더에 영구 보존.
