# 런타임 로그(파일 기록) — LogRules & RuntimeLogger 패턴

## 규칙 요약 (`Assets/_Project/Docs/LogRules.md`)
- **raw `Debug.Log`/`Debug.LogWarning`를 진단 로그로 쓰면 규칙 위반** — 콘솔에만 남아 파일화 안 됨 → Claude가 못 읽음.
- 반드시 `RuntimeLogger` 경유(콘솔+파일 동시). 에디터는 `_Logs/`에 파일 저장, 실기기는 파일 없이 Logcat만.
- 형식(자동생성): `[HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value`. 레벨(Info/Warn/Error) 필수, 카테고리 `[System/Class]` 필수.
- 파일 경로: `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_작업명/RuntimeLog_host.txt|_client.txt`. **로그 코드는 테스트 후 제거 대상(주석 `[테스트 진단 로그 — 제거 예정]` 태그 유지), 로그 파일 자체는 보존.**
- 스팸 방지: 상태 "전이"에서만 로깅(틱마다 금지). 민감정보(ID/토큰) 금지.

## RuntimeLogger API (`Infrastructure/Debug/RuntimeLogger.cs`, static)
- `BeginSession(folderPath, role)` — role="host"→RuntimeLog_host.txt, 그 외→_client.txt. append+autoflush. 중복 호출 안전(내부 EndSession 선행).
- `Log(LogLevel, system, class, message, data=null)` — 콘솔 항상 + 에디터 파일(세션 열려있으면). BeginSession 없이 호출해도 콘솔만 안전.
- `EndSession()` — 파일 핸들 닫기. null 안전.
- `LogLevel`은 `Hexiege.Infrastructure` enum(Info/Warn/Error).

## Application 레이어에서 파일 로그 남기는 법 (의존성 역전 — 2026-08-04 확립)
Application→Infrastructure 직접 참조 금지라 RuntimeLogger를 직접 못 씀. 프로젝트 기존 패턴(IUnitFactory/IGameServices)과 동일하게:
1. **인터페이스+중립 enum** `Assets/_Project/Scripts/Application/Interfaces/IRuntimeLogSink.cs`:
   - `enum LogLevel { Info, Warn, Error }` (Application 네임스페이스 — Infra enum 참조 회피)
   - `interface IRuntimeLogSink { void Log(LogLevel, string system, string className, string message, string data=null); }`
   - 세션 제어(BeginSession/EndSession)는 인터페이스에 **넣지 않음** — 역할 판별 가능한 상위(Bootstrap/Network)가 RuntimeLogger 직접 관리.
2. **어댑터** `Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLoggerSink.cs`:
   - `public sealed class RuntimeLoggerSink : Hexiege.Application.IRuntimeLogSink` — 내부에서 `RuntimeLogger.Log(MapLevel(level),...)`.
   - Application.LogLevel → Infrastructure.LogLevel 1:1 매핑(`MapLevel`).
3. **주입** `GameBootstrapper.Setup.cs`: `var sink = new RuntimeLoggerSink(); _statusEffectSystem.SetLogSink(sink); _unitCombat.SetLogSink(sink);`
4. **로그 지점**(Application): `private IRuntimeLogSink _log;` + `_log?.Log(LogLevel.Info, "Skill", nameof(StatusEffectSystem), "Apply", $"unit={id}, ...");` (null이면 무동작 — `?.`가 인자 평가까지 단락).
- Infrastructure 클래스(NetworkSkillController 등)는 어댑터 없이 `RuntimeLogger.Log(...)` 직접 호출 가능.

## enum 이름 충돌 주의 (핵심)
- `LogLevel`이 `Hexiege.Application`·`Hexiege.Infrastructure` 양쪽 존재.
- 파일이 속한(enclosing) 네임스페이스의 멤버가 `using`으로 들여온 것보다 **우선** → CS0104(모호) 안 남. (모호는 서로 다른 두 using에서 같은 이름이 올 때만.)
  - 예: `namespace Hexiege.Infrastructure`인 NetworkSkillController가 `using Hexiege.Application;`여도 `LogLevel`=Infrastructure.LogLevel로 해석(정상).
- **어댑터에서 인터페이스 시그니처 매칭 시엔 fully-qualify 필수**: `public void Log(Hexiege.Application.LogLevel level, ...)` — unqualified로 쓰면 enclosing(Infrastructure) LogLevel로 해석돼 인터페이스 구현이 안 됨.

## 세션(BeginSession/EndSession) 배선 위치
- **멀티플레이**: `NetworkCombatController.OnNetworkSpawn`(NetworkContext.Set 직후, `IsServer?"host":"client"`) / `OnNetworkDespawn`(NetworkContext.Reset 직후 EndSession). 각 인스턴스가 자기 역할 파일로 기록.
- **싱글플레이**: `GameBootstrapper.Start()` 싱글 분기에서 `BeginSession(folder,"host")` + `_dbgSessionOwned=true`, `OnDestroy`에서 `if(_dbgSessionOwned) EndSession()`. 소유권 가드로 멀티 세션과 충돌 방지.
- 폴더 경로 상수는 세션 여는 파일(GameBootstrapper.cs, NetworkCombatController.cs)에 각각 `const string`으로 둠(제거 시 함께 삭제).

## 테스트 방법
- **에디터에서 호스트로 실행해야 파일 생성됨**(실기기는 파일 미생성=Logcat만). 멀티는 에디터=호스트 → RuntimeLog_host.txt, 상대 클라 에디터 → RuntimeLog_client.txt.
