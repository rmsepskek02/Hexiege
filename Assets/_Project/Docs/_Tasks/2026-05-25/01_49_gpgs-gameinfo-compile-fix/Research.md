# Research — GPGS GameInfo.cs 컴파일 에러 수정

## 이 작업은 무엇인가?

Google Play Games SDK(GPGS)에서 자동 생성되는 `GameInfo.cs` 파일이 삭제되어
Unity 컴파일이 실패하고 있는 문제를 파악한 내용입니다.
이 파일은 원래 GPGS Android Setup 과정에서 자동으로 생성되는 파일인데,
이전 작업(`Firebase/GPGS SDK gitignore 처리 및 추적 해제`, 커밋 `0e78671`)에서
gitignore 처리 후 로컬에서도 삭제된 것으로 보입니다.

---

## 에러 발생 위치

Unity 콘솔에서 4개의 컴파일 에러가 발생하고 있으며,
모두 아래 파일에서 `GameInfo` 클래스를 찾지 못해서 나타납니다.

- **파일**: `Assets/GooglePlayGames/com.google.play.games/Runtime/Scripts/Platforms/Android/AndroidClient.cs`

### 에러가 발생하는 4개의 참조 위치 (AndroidClient.cs 기준)

| 줄 번호 | 참조 내용 |
|---------|----------|
| 188 | `GameInfo.WebClientIdInitialized()` 호출 |
| 196 | `GameInfo.WebClientId` 사용 |
| 216 | `GameInfo.WebClientIdInitialized()` 호출 |
| 236 | `GameInfo.WebClientId` 사용 |

---

## 삭제된 파일 정보

- **삭제된 파일**: `Assets/GooglePlayGames/com.google.play.games/Runtime/Scripts/GameInfo.cs`
- **삭제된 메타 파일**: `Assets/GooglePlayGames/com.google.play.games/Runtime/Scripts/GameInfo.cs.meta` (git status에서 D로 확인)

이 파일은 GPGS가 App ID, Web Client ID 등 프로젝트별 설정값을 담아 자동 생성하는 파일입니다.
`#if UNITY_ANDROID` 조건 컴파일 블록 안에 있으며, Android 빌드에서만 활성화됩니다.

---

## 재생성 방법

GPGS는 이미 `template-GameInfo.txt` 파일을 제공하고 있습니다.
- **경로**: `Assets/GooglePlayGames/com.google.play.games/Editor/template-GameInfo.txt`

이 템플릿으로부터 `GameInfo.cs`를 재생성하면 컴파일 에러가 해소됩니다.
템플릿에는 `APP_ID`, `WEB_CLIENTID` 등의 자리 표시자가 포함되어 있으며,
이 상태 그대로 복사해도 컴파일은 통과합니다.
(실제 값이 비어 있어도 `WebClientIdInitialized()`가 false를 반환하는 방식으로 처리됩니다.)

---

## 영향 범위

| 대상 | 영향 |
|------|------|
| `AndroidClient.cs` | `GameInfo` 클래스를 4곳에서 참조 → 컴파일 에러 발생 |
| 다른 프로젝트 파일 | 영향 없음 (GameInfo는 GPGS 내부에서만 사용) |
| gitignore | `GameInfo.cs`는 이미 gitignore에 포함된 상태 → 재생성해도 git에 올라가지 않음 |

---

## 결론

`template-GameInfo.txt` 내용을 그대로 `Runtime/Scripts/GameInfo.cs`로 복사하여
재생성하면 컴파일 에러가 해소됩니다.
gitignore 처리된 파일이므로 로컬에만 존재하며, git에는 올라가지 않습니다.
