# Research — RuntimeLogger 유틸리티

## 작업 목적

프로젝트에 앞으로 계속 사용할 런타임 로그 기록 유틸리티를 만든다.
현재는 버그 발생 시 에디터 콘솔만 볼 수 있어서 실기기에서 재현된 버그의 흐름을 
클로드가 직접 추적하기 어렵다. 이를 해결하기 위해 에디터에서는 파일로, 
실기기에서는 Logcat으로 동일한 형식의 로그를 남기는 공용 유틸리티가 필요하다.

첫 사용 목적은 랜덤 매칭 버그 원인 파악용 디버그 로그 수집이다.

---

## LogRules.md 핵심 요약

### 출력 형식
```
[HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value, key=value
```

예시:
```
[10:53:23.124] [INFO] [Network/MatchmakerManager] DetermineIsHostAsync 진입 | matchId=abc123
[10:53:23.125] [WARN] [Network/NetworkGameManager] StartHost 직전 | IsListening=True
[10:53:23.126] [ERROR] [Network/NetworkGameManager] StartHost 실패 | IsListening=True, IsHost=False
```

### 로그 레벨
| 레벨 | 사용 시점 |
|------|---------|
| `[INFO]` | 정상 흐름 진입/완료/상태 확인 |
| `[WARN]` | 예상 밖이지만 동작 계속 |
| `[ERROR]` | 반드시 원인 파악이 필요한 오류 |

### 파일 저장 위치 (에디터)
```
Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/
  RuntimeLog_host.txt   ← Host 측 로그
  RuntimeLog_client.txt ← Client 측 로그
```

### 환경별 출력
| 환경 | 방식 |
|------|------|
| 에디터 | `_Logs/` 폴더에 파일 저장 |
| 실기기 | Logcat 출력 → 사용자가 복사하여 공유 |

---

## 현재 상태

- 런타임 로그 파일 쓰기 구현체 없음
- 로그가 필요할 때마다 `Debug.Log`로 임시 처리 → 에디터 콘솔에만 나오고 파일로 남지 않음
- 실기기 디버깅 시 Claude가 로그를 읽을 수 없어 원인 파악에 한계 존재

---

## 구현 방향

### 클래스 위치 및 레이어
- `Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs`
- Infrastructure 레이어 — UnityEngine 참조 가능, 외부 서비스와 무관한 유틸리티

### 핵심 설계
- **정적 클래스** — 어디서든 `RuntimeLogger.Log(...)` 형태로 호출
- **세션 초기화**: 로그 수집 시작 시 `RuntimeLogger.BeginSession(folderPath, role)` 호출
  - `role`: `"host"` 또는 `"client"` → 파일명 결정 (`RuntimeLog_host.txt` / `RuntimeLog_client.txt`)
  - `folderPath`: `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/`
- **환경 분기**:
  - `#if UNITY_EDITOR`: 파일에 append 저장
  - 실기기: `Debug.Log`로 동일 형식 출력 (→ Logcat)
- **세션 종료**: `RuntimeLogger.EndSession()` — 파일 닫기

### 메서드 시그니처 (안)
```csharp
RuntimeLogger.BeginSession(string folderPath, string role);
RuntimeLogger.Log(LogLevel level, string system, string className, string message, string data = null);
RuntimeLogger.EndSession();
```

호출 예시:
```csharp
RuntimeLogger.Log(LogLevel.Info, "Network", "NetworkGameManager",
    "StartMatchmakingAsync 진입", $"IsListening={NetworkManager.Singleton?.IsListening}");
```

### 주의사항
- 로그 출력 코드는 작업 완료 후 반드시 제거 (LogRules.md 규칙)
- 파일 쓰기는 에디터 전용 (`#if UNITY_EDITOR`) — 빌드에 포함되지 않도록
- `Application.persistentDataPath` 대신 `_Logs/` 프로젝트 내부 경로 사용 (에디터 한정)
- 민감한 데이터(사용자 ID, 인증 토큰) 출력 금지

---

## 영향 범위

| 항목 | 내용 |
|------|------|
| 신규 파일 | `Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs` |
| 수정 파일 | 없음 (이번 작업은 유틸리티만 생성) |
| 첫 사용처 | 랜덤 매칭 디버그 — `NetworkGameManager`, `MatchmakerManager` |
