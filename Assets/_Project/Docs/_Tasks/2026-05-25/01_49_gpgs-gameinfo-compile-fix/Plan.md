# Plan — GPGS GameInfo.cs 컴파일 에러 수정

## 이 작업은 무엇인가?

삭제된 `GameInfo.cs` 파일을 프로젝트에 이미 존재하는 템플릿(`template-GameInfo.txt`)을 기반으로 재생성합니다.
이 작업 하나로 4개의 컴파일 에러가 모두 해소됩니다.
새 파일은 gitignore에 의해 git에 올라가지 않으며, 로컬에서만 유지됩니다.

> **GameSystemRules.md 관련성**: 이 작업은 외부 SDK(GPGS) 파일 복원 작업이므로 GameSystemRules.md의 게임 시스템 규칙과 직접 연관되는 항목이 없습니다.

---

## 수정 항목

### [1] GameInfo.cs 생성

| 항목 | 내용 |
|------|------|
| **생성할 파일** | `Assets/GooglePlayGames/com.google.play.games/Runtime/Scripts/GameInfo.cs` |
| **기반** | `Assets/GooglePlayGames/com.google.play.games/Editor/template-GameInfo.txt` 내용 그대로 복사 |
| **수정 사항** | 없음 — 템플릿 내용을 변경 없이 그대로 사용 |
| **기존 파일 제거 여부** | 없음 — 이미 삭제된 상태 |

### 생성할 파일의 내용 (template-GameInfo.txt와 동일)

```csharp
#if UNITY_ANDROID
namespace GooglePlayGames {
    public static class GameInfo {
        private const string UnescapedApplicationId = "APP_ID";
        private const string UnescapedIosClientId = "IOS_CLIENTID";
        private const string UnescapedWebClientId = "WEB_CLIENTID";
        private const string UnescapedNearbyServiceId = "NEARBY_SERVICE_ID";

        public const string ApplicationId = "__APP_ID__";
        public const string IosClientId = "__IOS_CLIENTID__";
        public const string WebClientId = "__WEB_CLIENTID__";
        public const string NearbyConnectionServiceId = "__NEARBY_SERVICE_ID__";

        public static bool ApplicationIdInitialized() { ... }
        public static bool IosClientIdInitialized() { ... }
        public static bool WebClientIdInitialized() { ... }
        public static bool NearbyConnectionsInitialized() { ... }
    }
}
#endif
```

---

## 예상 위험 요소

| 항목 | 내용 |
|------|------|
| App ID 미설정 | 템플릿 상태로 복원하면 App ID가 자리 표시자(`__APP_ID__`) 그대로 남음. 컴파일은 통과하지만 실제 Android 빌드 및 GPGS 기능은 동작하지 않음. 이는 기존 개발 환경과 동일한 상태임. |
| gitignore 처리 | 재생성된 파일은 gitignore에 의해 추적되지 않음. 팀 작업 시 다른 개발자도 동일하게 재생성해야 함. |
| 기타 파일 | 다른 파일은 수정하지 않음. 영향 없음. |

---

## 작업 순서

1. `template-GameInfo.txt` 내용을 `Runtime/Scripts/GameInfo.cs`로 복사하여 생성
2. Unity에서 스크립트 재컴파일 확인
3. 4개 컴파일 에러 해소 확인
