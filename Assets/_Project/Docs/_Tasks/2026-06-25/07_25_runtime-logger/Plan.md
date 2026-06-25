# Plan — RuntimeLogger 유틸리티

## 작업 목적

에디터에서는 `_Logs/` 폴더에 파일로, 실기기에서는 Logcat으로 동일한 형식의
런타임 로그를 남길 수 있는 공용 유틸리티 클래스를 만든다.

---

## 수정 항목

### [신규] `Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs`

**GameSystemRules 근거**: 없음. 아키텍처 제약(MEMORY.md) 준수.
- Infrastructure 레이어 — UnityEngine 참조 가능, 게임 로직과 무관한 유틸리티
- 정적 클래스 — 의존성 주입 없이 어디서든 호출 가능

**구현 내용:**

```
BeginSession(folderPath, role)
  - role: "host" 또는 "client" → 파일명 결정
  - folderPath: "_Logs/YYYY-MM-DD/HH_MM_[작업명]/"
  - #if UNITY_EDITOR: 해당 경로에 파일 생성 후 헤더 작성
  - 실기기: 아무것도 안 함 (Logcat은 Log() 호출 시 항상 출력)

Log(level, system, className, message, data)
  - 형식: [HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | data
  - #if UNITY_EDITOR: 파일에 append
  - 항상: Debug.Log로도 출력 (에디터 콘솔 + 실기기 Logcat 동시 대응)

EndSession()
  - #if UNITY_EDITOR: 파일 스트림 닫기
```

**LogLevel 열거형:**
```
Info, Warn, Error
```

**주의사항:**
- 파일 쓰기 코드는 `#if UNITY_EDITOR` 안에서만 — 빌드에 포함되지 않도록
- `StreamWriter`는 `BeginSession`에서 열고 `EndSession`에서 닫음
- `BeginSession` 없이 `Log`를 호출하면 Debug.Log만 출력 (파일 쓰기 없이 안전하게 동작)
- 민감한 데이터(사용자 ID, 인증 토큰) 출력 금지

---

## 호출 방법 (사용 예시)

```csharp
// 세션 시작 (매칭 시작 시점 등)
RuntimeLogger.BeginSession("Assets/_Project/Docs/_Logs/2026-06-25/07_25_matchmaking-debug", "host");

// 로그 출력
RuntimeLogger.Log(LogLevel.Info, "Network", "NetworkGameManager",
    "StartMatchmakingAsync 진입", $"IsListening={NetworkManager.Singleton?.IsListening}");

RuntimeLogger.Log(LogLevel.Warn, "Network", "MatchmakerManager",
    "플레이어 목록 조회 완료", $"count={sortedPlayers.Count}, isHost={isHost}");

// 세션 종료
RuntimeLogger.EndSession();
```

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| 파일 경로 | `Assets/_Project/Docs/_Logs/` 경로는 에디터 전용. 빌드에서 접근 불가 → `#if UNITY_EDITOR`로 보호 |
| 스트림 미종료 | `EndSession()` 미호출 시 파일이 손상될 수 있음 → `BeginSession`에서 기존 스트림 자동 정리 |
| 로그 코드 잔류 | 작업 완료 후 `BeginSession` / `Log` / `EndSession` 호출 코드 반드시 제거 |

---

## 작업 순서

1. `Assets/_Project/Scripts/Infrastructure/Debug/` 폴더 생성
2. `RuntimeLogger.cs` 작성
3. 컴파일 확인

---

## 추가 수정 항목 (2026-06-25)

### [수정] `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs` — `_networkGameManager` 자동 탐색

**근거**: 디버그 로그로 발견한 로비 복귀 시 NGO Shutdown 누락 버그(Research.md 참조).

**문제**: `_networkGameManager`가 Inspector 미연결로 항상 null → 로비 복귀 시
`NetworkGameManager.BackToLobby()`(Shutdown 포함)가 호출되지 않음.
`NetworkGameManager`는 `DontDestroyOnLoad` 오브젝트라 Game 씬 인스펙터에서 연결 불가.

**수정 내용**: `Initialize()` 서두에서 `_networkGameManager`가 null이면
`FindFirstObjectByType<NetworkGameManager>()`로 자동 탐색.
멀티플레이(`NetworkContext.IsNetworkActive`)인데도 못 찾으면 `Debug.LogError`로 경고.
(`LobbyUI.cs` line 97~104의 기존 패턴과 동일)

```csharp
if (_networkGameManager == null)
    _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

if (_networkGameManager == null && NetworkContext.IsNetworkActive)
    Debug.LogError("[Network] GameEndUI: NetworkGameManager를 찾을 수 없습니다. ...");
```

**주의**: Presentation 레이어이므로 `FindFirstObjectByType` 사용 가능(레이어 규칙 준수).
수정 파일은 `GameEndUI.cs` 한 개뿐.
