# game-programmer — 프로젝트 지식

## 최우선 함정

### `Application` 네임스페이스 충돌 (CS0234)
`Hexiege.Application` 네임스페이스가 존재한다. `namespace Hexiege.*` 안의 파일에서
수식 없는 `Application` 은 **`UnityEngine.Application` 이 아니라** `Hexiege.Application` 으로 해석된다.
(C# 은 `using` 보다 자기를 감싸는 네임스페이스를 먼저 찾는다)
→ **항상 `UnityEngine.Application` 으로 완전 수식.**
실제 사고: `Assets/Editor/Tools/LogcatCapture.cs` CS0234 3건 (커밋 `9fcee6b7` 로 해소).
같은 이유로 `Hexiege.Infrastructure.LogLevel` vs `Hexiege.Application.LogLevel` 도 충돌 →
`FileSink.cs` 는 인터페이스 구현 자리에서 완전 수식한다.

## 로그 시스템 (LogRules.md 소관)

규격 문서: `Assets/_Project/Docs/LogRules.md` — 축 A(심각도) × 축 B(존속: 운영/개발/임시) 2축 판정.
판정 이력표: `Assets/_Project/Docs/LogAudit.md` (205행).

| 역할 | 파일 |
|---|---|
| 정적 facade | `Assets/_Project/Scripts/Application/GameLog.cs` (`Ops` / `Dev` 중첩 클래스, `HashId`, `Sanitize`) |
| sink 인터페이스 + `LogEvent` enum | `Assets/_Project/Scripts/Application/Interfaces/ILogSink.cs` |
| 콘솔 구현 | `Assets/_Project/Scripts/Infrastructure/Debug/ConsoleSink.cs` |
| 파일 구현 | `Assets/_Project/Scripts/Infrastructure/Debug/FileSink.cs` (전체가 `#if UNITY_EDITOR`) |
| 파일 쓰기 실체 | `Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs` |
| sink 등록 | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` `InitializeLogging()` |

### `GameLog` 시그니처 (인자 순서 실수가 실제로 4건 났다)
```csharp
GameLog.Ops.Error(LogEvent e, string system, string className, string message, string data = null)
GameLog.Ops.Error(LogEvent e, string system, string className, string message, Exception ex, string data = null)
GameLog.Dev.Info(string system, string className, string message, string data = null)   // LogEvent 없음
```
- `Ops` = 운영(릴리스 존속, `LogEvent` 키 필수, `[Conditional]` 금지)
- `Dev` = 개발(`UNITY_EDITOR` + `DEVELOPMENT_BUILD` 두 개 모두 부착 → 릴리스에서 소거)

### 민감 데이터 (1.6)
- UID·PlayerId 는 `GameLog.HashId(...)` 를 거쳐서만 로그에 넣는다 (1단 방어).
- **`Lobby.HostId` 는 UGS PlayerId 그 자체다** — `LobbyManager.cs` 의 `IsHost` 가
  `CurrentLobby.HostId == AuthenticationService.Instance.PlayerId` 로 직접 비교한다. 로그에 넣을 때 반드시 해시.
- MatchId / Lobby Name 은 `LogAudit.md` §7 이 "준식별자 — 규정 항목 아님, 미확인으로 남긴다" 로 분류. **판단하지 말고 둘 것.**
- 미적용 잔여분은 코드에 `⚠️ 5단계(마스킹) 대상` 주석으로 표시되어 있어 grep 으로 찾을 수 있다.

### 에디터 로그 경로
`Assets/_Project/Docs/_Logs/_editor/{yyyy-MM-dd}/RuntimeLog.txt` (일 단위 이어쓰기).
2026-08-14 개정으로 파일명에서 역할(host/client)이 빠졌다 — 옛 `RuntimeLog_host.txt` 참조 주석이 남아 있으면 정정 대상.
**단 실기기 캡처 `RuntimeLog_device.txt` 는 별개 규정(1.12)이라 그대로 유지한다.**
경로 계산은 `FileSink.EditorLogsRootRelativeToAssets`("_Project/Docs/_Logs/_editor") + `UnityEngine.Application.dataPath`.
→ 이 상수는 `private` 이라 에디터 도구에서 참조 불가. `LogcatCapture.cs` 가 같은 값을 복제하고 있으니 **바꿀 때 둘 다** 고칠 것.

## 에디터 도구 — 파괴적 동작 규약

`Assets/Editor/Tools/LogcatCapture.cs` — 메뉴 3개
(`1. 버퍼 비우기` / `2. 파일로 저장` / `3. 오래된 에디터 로그 정리`).

삭제를 다루는 코드는 이 프로젝트의 사고 이력(2026-03-03 `git restore` 무단 실행으로 미커밋 작업 전체 소실
→ CLAUDE.md 규칙 5) 때문에 아래를 반드시 지킨다:

1. **자동·주기 실행 금지.** 사용자가 메뉴를 직접 누를 때만.
2. **삭제 대상 목록을 먼저 보여 주고** 확인받는다. 목록이 길면 잘라도 **총 개수는 항상** 표시.
3. **경로 가드**: `Path.GetFullPath` 로 정규화(`..` 탈출 차단) → 뿌리 자체 거부 → `뿌리 + "/"` prefix 검사
   (구분자를 붙여야 `_editor_backup` 같은 형제 폴더가 통과하지 않는다) → 뿌리 직속 자식인지(`remainder` 에 `/` 없음).
   **삭제 루프 안에서 매 건 재검사**하고, 한 건이라도 걸리면 전체 중단.
4. **판정은 날짜 폴더 이름 기준.** 파일 개수(`최근 N개`) 기준 금지 — 정렬이 어긋나면 최신 파일을 지운다.
   이름이 `yyyy-MM-dd` 로 파싱되지 않는 폴더는 "판정 불가 = 삭제 불가".
5. `Assets/` 하위 삭제는 **`AssetDatabase.DeleteAsset(프로젝트 상대경로)`** 를 쓴다 —
   `.meta` 를 함께 지운다. `FileUtil.DeleteFileOrDirectory` 는 대상만 지워 짝 잃은 `.meta` 가 남는다
   (이 프로젝트는 `_Tasks`/`_Logs` 문서의 `.meta` 도 커밋한다). 실패 시 폴백으로 `FileUtil` + `.meta` 명시 삭제.
6. 삭제 후 `AssetDatabase.Refresh()`.
7. 유니티에는 텍스트 입력 대화상자가 없다. 되돌릴 수 없는 동작의 임계값은
   `EditorUtility.DisplayDialogComplex` 로 **미리 정한 보수적 값 중 선택**하게 한다(자유 입력은 0/1 입력 사고를 부른다).
   `DisplayDialogComplex` 반환: 0=ok, 1=cancel, 2=alt.

## 작업 규약 (CLAUDE.md에서 특히 자주 걸리는 것)

- **git 명령 일체 금지** (`git status` 포함, 사용자가 명시하지 않는 한).
- 요청 범위 밖은 **고치지 말고 보고만** 한다.
- 주석은 유니티 초급자 기준으로 상세히 — 기존 파일의 주석 밀도(왜 그렇게 했는지 근거까지)를 따라간다.
- 추정 금지. 근거가 되는 파일·행을 확인하고 답한다.
