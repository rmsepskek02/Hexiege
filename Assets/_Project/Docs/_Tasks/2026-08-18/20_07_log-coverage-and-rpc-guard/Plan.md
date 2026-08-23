# Plan — 전역 로그 훅의 Error 수집 + 전투 컨트롤러 RPC 가드 + 연구 흐름 로그 보강

작성일: 2026-08-18 20:07
대상 작업 3건 (B 로그 수집 범위 확대 / A 동작 버그 수정 / C 진단 로그 추가)
근거 로그: `Assets/_Project/Docs/_Logs/_editor/2026-08-18/RuntimeLog.txt` (753줄)

---

## [최상단] 기존 로직 제거 — **제거 0건** (WORKFLOW.md [4] 「기존 로직 제거 규칙」)

이번 작업에는 **삭제하거나 주석 처리하는 기존 로직이 하나도 없다.** 세 건 모두 「가드 추가」 · 「조건 완화」 · 「로그 추가」뿐이다.

다만 규칙의 취지상 **최상단에 먼저 밝혀야 할 변경**이 하나 있다. 제거는 아니지만 **기존 방어 한 겹을 느슨하게 만드는** 변경이기 때문이다.

| 항목 | 위치 | 무엇이 달라지나 |
|---|---|---|
| 전역 로그 훅의 「방어 1」 조건 | `Infrastructure/Debug/LogSessionOwner.cs` **314행** `if (type != UnityEngine.LogType.Exception) return;` | `Exception` **또는** `Error` 를 통과시키도록 **조건을 넓힌다.** 줄을 지우는 것이 아니라 조건을 바꾼다 |

- 이 방어 1은 지금까지 **되먹임(무한 루프) 고리를 원천 차단**하는 역할을 겸하고 있었다. 조건을 넓히면 그 역할이 아래 방어 겹들로 넘어간다.
- 그래서 **§3 에서 ① 원래의 배제 근거(이중 집계)가 왜 소멸했는지, ② 되먹임 경로를 코드로 끝까지 따라간 결과, ③ 대체 방어를 무엇으로 둘 것인지**를 먼저 확정한 뒤에 이 줄을 손댄다.
- 되돌리기는 **조건 한 줄을 원래대로 복원**하면 끝이라, 롤백 비용이 사실상 0이다.

---

## 이 작업이 무엇이고 왜 하는가 (자연어 설명 — CLAUDE.md 규칙 13)

이번에 실기 테스트(에디터 + 실기기 랜덤매칭)를 하면서 **서로 다른 세 가지 문제**가 함께 드러났다. 셋 다 「로그로 무엇을 알 수 있는가」와 「게임이 끝난 뒤에도 통신을 시도한다」는 두 축에 걸려 있어 한 작업으로 묶는다.

**첫째(B), 우리 기록 장치가 남의 오류를 못 듣고 있다.** 이 프로젝트는 게임이 실행되는 동안 일어난 일을 파일로 남겨 두고, 나중에 그것을 읽어 원인을 찾는다. 그런데 이 기록 장치는 지금 **"프로그램이 예외로 멈춘 사건"만** 주워 담고, **엔진·통신 라이브러리 같은 바깥 부품이 스스로 뱉는 오류 메시지는 통째로 흘려보낸다.** 이번 테스트에서 실제로 통신 관련 오류가 났는데, 753줄짜리 로그 파일에는 오류 줄이 **한 줄도 없다.** 앞으로 출시 후에 "플레이어 기기에서만 나는 문제"를 서버로 모아 보겠다는 것이 이 로그 시스템의 목적인데, **바깥 부품이 내는 오류가 전부 빠지면 그 목적의 절반이 비어 있는 셈**이다. 그래서 이번에 그 오류들도 함께 담게 만든다.

다만 이 변경은 조심해서 해야 한다. **우리 기록 장치가 로그를 남기는 방식 자체가 "오류를 뱉는 것"이기 때문**이다. 그래서 아무 생각 없이 "오류를 전부 담자"고 하면, 우리가 남긴 오류를 우리가 다시 주워 담고, 그것을 남기면서 또 오류를 뱉는 **끝없는 되풀이**에 빠져 게임이 멈춘다. 이 계획서에서 가장 공을 들인 부분이 바로 그 되풀이가 생기지 않는다는 것을 **코드 경로를 끝까지 따라가서 확인하고, 그래도 남는 위험에 대비책을 세우는 일**이다.

**둘째(A), 게임이 끝나고 로비로 돌아가는 순간 통신 오류가 난다.** 승패가 갈린 뒤 [로비로] 버튼을 누르면 통신 연결이 끊기는데, 그 직후에도 전투를 계산하는 부분이 한 번 더 돌아서 **이미 끊긴 연결로 메시지를 보내려다 오류**를 낸다. 게임 진행에는 지장이 없지만 매번 오류가 남고, 실기에서 2회 재현됐다. 고치는 방법은 "내가 서버인가"만 보던 조건에 **"이 부품이 아직 살아 있는가"** 를 한 줄 더 얹는 것이다. 같은 프로젝트 안에 이미 같은 방식으로 처리한 자리가 두 곳 있어서 새 설계가 아니다.

**셋째(C), 연구(유닛 강화) 흐름에 로그가 없다.** 연구는 버튼을 눌러 시작하고, 시간이 지나면 완료되고, 그 결과가 상대 화면에도 반영되어야 한다. 그런데 이 전체 흐름 중 **성공하는 경로에는 로그가 한 줄도 없다.** 바로 직전 작업에서 고친 버그가 정확히 "완료 사실이 상대에게 전달되지 않는다"는 것이었는데, 그 지점에 로그가 없어서 **고쳐졌는지 확인할 방법이 없다.** 이번 753줄 로그에도 연구소를 지은 기록만 있고 연구 자체는 흔적이 없다. 그래서 착수·완료·반영 세 지점에 로그를 넣는다.

---

## 1. 배경 — 세 건이 어떻게 드러났는가

2026-08-18 실기 테스트(에디터 host + 실기기 client, 랜덤매칭 2회)에서 함께 관측되었다.

| 건 | 성격 | 관측 사실 |
|:-:|---|---|
| **B** | 로그 수집 구멍 | 게임 종료 시 `Rpc methods can only be invoked after starting the NetworkManager!` (`Debug.LogError`) 가 콘솔에 났으나, **로그 파일에는 없다.** 실측: 753줄 중 `[ERROR]` **0건**, `[WARN]` **2건**(둘 다 `ReconnectionHandler` — 308·740행) |
| **A** | 동작 버그 | 위 오류의 발신 지점이 `NetworkCombatController.StopCombatClientRpc` 다. 게임 종료 → [로비로] 경로에서 **2회 재현** |
| **C** | 진단 사각지대 | 같은 로그에 `BuildingType=Research` 건물 배치는 **6건**(93·96·440·448·461·466행) 남았는데, **연구 착수·완료·반영은 0건**이다 |

**로그 파일이 담고 있는 종료 흐름(1회차):**

```
[22:38:35.123] 서버: 게임 종료 감지 — 결과 전파 시작 | WinnerTeam=Red …
[22:38:52.896] [WARN] 클라이언트 연결 끊김 — 재접속 대기 시작 | ClientId=1 …
[22:38:55.835] NetworkManager Shutdown 완료
[22:38:55.843] 디스폰 — 구독 해제 완료   ← NetworkResourceSync
```

즉 **게임 종료 감지(22:38:35)와 Shutdown(22:38:55) 사이 약 20초 동안 전투 루프가 계속 돌고 있었고**, Shutdown 직후에 A 의 오류가 났다. 이 20초 구간은 §10 「범위 밖」의 근거이기도 하다.

---

## 2. 근거 규칙 확인 (WORKFLOW.md [4])

`Assets/_Project/Docs/GameSystemRules.md` 인덱스를 먼저 읽고, 관련 하위 문서를 확인했다.

| 건 | 근거 규칙 | 내용 |
|:-:|---|---|
| **A** | `GameSystemRules_Units.md` **규칙 29 · 34 · 40** | 서버 틱 진입점은 *"싱글=`GameBootstrapper.Update`(`!IsNetworkMode` 가드), 멀티=`NetworkCombatController`(IsServer 가드). 이중 틱 금지"* |
| **A** | `GameSystemRules_Buildings.md` **방어 타워 시스템 규칙 9** | 타겟 선택·사거리 판정·데미지 처리는 **서버에서만** 실행 (이 컨트롤러가 타워 틱을 구동한다 — `NetworkCombatController.cs` 339~340행) |
| **A** | `GameSystemRules_Upgrade.md` **규칙 7** | 자연회복 틱 진입점도 동일 — *"멀티=`NetworkCombatController`(IsServer), 이중 틱 금지"* |
| **C** | `GameSystemRules_Upgrade.md` **규칙 8 · 9** | 연구소 운영 / 서버 권위 네트워크 처리 — *"완료 레벨 = 양 클라 브로드캐스트 / 진행 상태 = 소유 클라 한정"* |
| **B** | **해당 없음** | `GameSystemRules.md` 인덱스의 13개 문서 어디에도 **로그에 관한 규칙이 없다.** B 의 근거는 전부 `LogRules.md` 다 |

**A 에 대한 중요한 단서:** 위 세 규칙은 모두 **`IsServer` 가드만** 규정하고 있고, **`IsSpawned`(오브젝트 생존)는 어느 규칙도 다루지 않는다.** 따라서 이번 A 수정은 **규칙을 바꾸는 것이 아니라, 규칙이 침묵하는 지점을 보강하는 것**이다. 규칙 문서 수정은 필요하지 않다.

**B 의 근거 규칙(LogRules.md):** **1.2**(두 축) · **1.5**(이벤트 키 신설 기준) · **1.9**(전역 미처리 예외 수집 · 재진입 가드 필수) · **1.14 금지 8**(매 틱 로깅 금지) · **1.14 금지 9**(같은 사건 두 곳 로깅 금지).

---

## 3. B — 전역 훅이 `LogType.Error` 를 놓친다 (이번 작업의 핵심)

### 3-1. 문제

`Infrastructure/Debug/LogSessionOwner.cs` 의 `OnUnityLogMessageReceived`(305~349행)가 **`LogType.Exception` 만** 수집한다.

```csharp
// 314행 — 방어 1
if (type != UnityEngine.LogType.Exception) return;
```

그 결과 **엔진 · NGO · 플러그인이 내는 `Debug.LogError` 는 파일에 한 줄도 남지 않는다.** 이번 테스트에서 실제로 발생한 오류:

```
Rpc methods can only be invoked after starting the NetworkManager!
UnityEngine.Debug:LogError (object)
Hexiege.Infrastructure.NetworkCombatController:StopCombatClientRpc (int) (…:1014)
```

`LogRules.md` **1.1** 이 규정한 두 목적 중 **「라이브 운영 지표 수집」이 엔진 계층에서 통째로 비어 있는 상태**다.

### 3-2. `Error` 배제는 의도적이었다 — 그러나 그 전제가 소멸했다

같은 파일 311~313행 주석이 배제 근거를 적어 두었다.

> *"(`Debug.LogError` 로 남는 **기존 로그**를 여기서 다시 수집하지 않는 이유이기도 하다. 같은 사건을 두 번 기록하면 서버 집계에서 발생 건수가 부풀려진다 — `LogRules.md` 1.3 분류 원칙 1.)"*

**작성 당시에는 프로젝트 코드가 raw `Debug.LogError` 를 널리 쓰고 있었다.** 지금은 누적 **386건**의 이관이 끝나(`LogRules.md` **1.13** 「2026-08-18」 절) 게임 런타임 코드의 raw 호출이 **0건**이다.

**실측 (2026-08-18, `Assets/_Project/Scripts` 전체 `Debug.LogError`):**

| 위치 | 건수 | 성격 |
|---|:-:|---|
| `Infrastructure/Debug/RuntimeLogger.cs` **171행** | 1 | 로그 시스템 본체 (파일 sink 의 콘솔 출력) |
| `Infrastructure/Debug/ConsoleSink.cs` **60행** | 1 | 로그 시스템 본체 (빌드 sink) |
| `Application/GameLog.cs` **541행** | 1 | 로그 시스템 본체 (sink 0개일 때의 콘솔 폴백) |
| `Scripts/Editor/CombatHitEventInjector.cs` 80·199행 | 2 | 에디터 도구 — **플레이 모드 밖**이라 훅이 걸려 있지 않다 |
| `Infrastructure/Network/NetworkGameManager.cs` 714행 | (0) | **`/* */` 블록 주석 안**의 죽은 코드 (`logging.md` 실측과 일치) |

→ **"우리 코드가 낸 `Error` 를 훅이 다시 주워 이중 집계한다"는 상황은 더 이상 존재하지 않는다.** 남은 것은 **로그 시스템 자신의 출력**뿐이고, 그것은 이중 집계가 아니라 **되먹임** 문제다(3-3).

### 3-3. 되먹임 위험 — 코드 경로를 끝까지 따라간 결과

**실측 경로 (파일·행 번호 전부 확인):**

```
GameLog.Ops.Error(...)                      Application/GameLog.cs 172~187행
  → Emit(...)                               GameLog.cs 472행
      478행  if (_isEmitting) return;       ← 재진입 가드
      480행  _isEmitting = true;            ← sink 호출 "전에" 세운다
      508행  sink.Write(...)                ← try 블록 안, 동기 호출
  → FileSink.Write                          Infrastructure/Debug/FileSink.cs 184~205행
  → RuntimeLogger.Log                       Infrastructure/Debug/RuntimeLogger.cs 131행
      171행  Debug.LogError(line);          ← 여기서 훅이 되불린다
  → UnityEngine.Application.logMessageReceived 발화
  → OnUnityLogMessageReceived               LogSessionOwner.cs 305행
      523행(GameLog.cs) finally 로 _isEmitting = false  ← Emit 이 끝난 뒤에야 내려간다
```

**핵심:** `_isEmitting = true` 는 **480행(sink 호출 전)** 에 세워지고 **523행 `finally`(sink 호출 후)** 에 내려간다. `Debug.LogError`(171행)는 그 사이에서 **동기 호출**된다. 따라서 훅이 되불리는 시점에 `GameLog.IsEmitting` 은 **참**이다.

### 3-4. 질문 1 — 방어 2(`IsEmitting`)만으로 충분한가

**답: 주 경로는 충분하다. 단 유보 1건과 구멍 1건이 있다.**

| 경로 | `IsEmitting` 이 참인가 | 근거 |
|---|:-:|---|
| `GameLog.Ops/Dev.*` → `FileSink` → `RuntimeLogger.Log` → `Debug.LogError` | **참** | 위 3-3 (480행 ↔ 523행 사이) |
| `GameLog.*` → `ConsoleSink.Write` → `Debug.LogError`(60행) | **참** | 같은 `Emit` 안. `ConsoleSink.cs` 79~81행 주석이 이미 그렇게 적고 있다 |
| `GameLog.*` → `FallbackToConsole`(sink 0개) → `Debug.LogError`(541행) | **참** | `Emit` 의 `try` 블록 안(493~497행) |
| **`RuntimeLogger.Log(LogLevel.Error, …)` 직접 호출** | **거짓** | `GameLog` 를 거치지 않는다 — **구멍** |

**유보 1 (확인 필요):** Unity 가 `logMessageReceived` 를 **같은 콜스택에서 동기로** 발화한다는 것은 이 리포지토리만으로 확정할 수 없다(엔진 소스가 세션에 없다). 다만 이것은 **새로 생기는 유보가 아니다** — 현행 코드의 「방어 3」(323~327행)이 존재하는 이유가 정확히 *"훅 전달이 혹시라도 즉시가 아니라 나중에 이루어지는 경우"* 이며, 주석에 그렇게 적혀 있다. 즉 **프로젝트는 이미 이 불확실성을 전제로 설계되어 있다.**

**구멍 1 (실측으로 확인):** `LogRules.md` **1.11** 은 축 B `임시` 로그에 대해 **`RuntimeLogger` 직접 호출을 명시적으로 허용**한다. 그 경로는 `GameLog` 를 거치지 않으므로 `IsEmitting` 이 거짓이다.

- **현재 위험은 0이다** — 실측 결과 `Infrastructure/Debug/` 밖에서 `RuntimeLogger.Log(` 를 호출하는 자리는 **0건**이다.
- **그러나 규칙이 허용하는 경로**라 언제든 생길 수 있다.
- 그 경우 **무한 루프는 아니다.** 경로를 따라가면: 직접 호출 → `Debug.LogError` → 훅(IsEmitting 거짓, 통과) → `GameLog.Ops.Error` → `Emit`(플래그 ON) → sink → `Debug.LogError` → 훅(**IsEmitting 참 → 반환**) 에서 멈춘다. **결과는 "같은 사건 2줄" = `LogRules.md` 1.14 금지 9 위반**이다.

→ 따라서 **방어 2 만으로 끝내지 않는다.**

### 3-5. 질문 2 — 우리 출력과 외부 오류를 확실히 가르는 방법

| 후보 | 방식 | 장점 | 단점 | 판정 |
|:-:|---|---|---|:-:|
| **(a)** | 방어 2·3 에만 의존 | 코드 변경 최소(조건 한 줄) | ① 3-4 의 **구멍 1을 못 막는다** ② **방어 3이 약하다** — 우리 로그 라인에는 `[HH:MM:SS.fff]` 가 들어가 **메아리마다 문자열이 달라져** 「직전과 동일」 비교가 영원히 성립하지 않는다 | 기각 |
| **(b)** | 로그 라인 **접두사로 필터** (`[시각] [ERROR] [System/Class]` 형태면 우리 것으로 간주) | 새 상태 없음. 발신 주체와 무관하게 걸러진다 | **형식과 결합된다** — `LogRules.md` **1.4** 의 형식이 바뀌면 필터가 **조용히** 깨진다(무한 루프 또는 진짜 오류 누락). 문자열 규약이 두 파일에 흩어지고 컴파일러가 검증해 주지 않는다 | 기각 |
| **(c)** | `RuntimeLogger` 에 **콘솔 출력 중 플래그**를 두고 훅이 그것을 읽는다 | ① 구멍 1(직접 호출)까지 덮는다 ② **메시지 형식과 무관** ③ 상태가 **출력하는 코드 바로 옆**에 있어 짝이 눈에 보인다 ④ **선례가 이미 있다** — `GameLog.IsEmitting`(74행)이 정확히 같은 패턴 | **채택** |

**채택안 (c) — 상세**

- `RuntimeLogger` 에 정적 플래그 `IsEmittingToConsole`(이름은 구현 시 확정, `GameLog.IsEmitting` 과 대칭되게 짓는다)을 두고, **`Log()` 의 콘솔 출력 `switch` 블록(165~176행)만** 감싼다. **`try` / `finally` 로 감싸 예외가 나도 반드시 내린다**(내려가지 않으면 이후 모든 훅 수집이 영구히 죽는다 — `GameLog.Emit` 의 `finally`(519~524행)와 같은 이유).
- 훅은 방어 2 옆에 한 줄을 더한다: `if (RuntimeLogger.IsEmittingToConsole) return;`
- **레이어:** `LogSessionOwner` 와 `RuntimeLogger` 는 **둘 다 `Hexiege.Infrastructure`** 다. 참조 방향 문제가 없다.
- **파일 쓰기(`_writer.WriteLine`)는 감싸지 않는다.** 파일 쓰기는 훅을 발화시키지 않으므로 플래그 구간을 넓힐 이유가 없다.

**최종 방어 구성 (4겹):**

| 겹 | 내용 | 무엇을 막는가 |
|:-:|---|---|
| 1 | `type` 이 `Exception` **또는** `Error` 가 아니면 반환 | Info/Warning 이 훅을 통과하는 것 (되먹임 표면 축소) |
| **1-b** | **`RuntimeLogger.IsEmittingToConsole` 이면 반환 (신설)** | `GameLog` 경유 + **직접 호출** 양쪽의 자기 출력 |
| 2 | `GameLog.IsEmitting` 이면 반환 (기존) | `ConsoleSink` · 폴백 경로 포함 `GameLog` 전 경로 |
| 3 | 직전과 동일한 `condition` + `stackTrace` 면 반환 (기존) | 훅 전달이 비동기일 경우의 1회 메아리 |

### 3-6. 질문 3 — `LogType.Assert` 는 어떻게 할 것인가

**답: 수집하지 않는다(제외).** 근거 셋.

1. **실측 사용처 0건** — `Assets/_Project/Scripts` 에서 `Debug.Assert` · `Assert.` 호출은 **0건**이다. 지금 켜도 아무것도 잡히지 않는다.
2. **릴리스에서 존재할 수 없다** — `Debug.Assert` 는 `UNITY_ASSERTIONS`(에디터 · 개발 빌드) 에서만 살아 있다. 즉 **릴리스 빌드에서는 발화 자체가 불가능**하므로, 이번 변경의 목적인 「라이브 운영 지표 수집」에 기여할 수 있는 양이 **원리적으로 0**이다.
3. **축 B ①이 항상 "아니오"** — 발화 가능한 환경이 에디터·개발 빌드뿐이라면 *"플레이어 기기에서만 벌어지는가"* 는 언제나 아니오다. `LogRules.md` **1.2** 축 B 기준으로 `운영` 이 될 수 없다.

> **재검토 조건:** 나중에 `Debug.Assert` 를 실제로 쓰기 시작하면 이 판정을 다시 연다. 근거 1·2 중 하나가 무너지기 때문이다.

### 3-7. 질문 4 — 훅이 잡은 `Error` 에 어떤 `LogEvent` 키를 줄 것인가

현행 훅은 `LogEvent.UnhandledException` 하나만 쓴다(343~348행). `LogRules.md` **1.5** 의 신설 기준 — *"집계가 섞이면 안 되고 **+** 조치가 다를 때"* **둘 다** 충족해야 한다 — 를 적용한다.

| 기준 | 판정 | 근거 |
|---|:-:|---|
| **집계가 섞이면 안 되는가** | **예** | `UnhandledException` 은 *"try-catch 로 감싸지 않은 곳에서 터진 예외"*(`ILogSink.cs` 83~88행 XML 주석)다. 엔진의 `Debug.LogError` 는 **예외가 아니다** — 스택이 풀리지도, 흐름이 중단되지도 않고 게임은 그대로 진행된다. 한 키로 묶으면 **크래시 건수 지표가 크래시 아닌 사건으로 부풀어**, 출시 판단에 쓰는 가장 중요한 숫자가 못 쓰게 된다 |
| **조치가 다른가** | **예** | 미처리 예외 → **그 자리에 `try-catch` 를 두거나 원인 코드를 고친다.** 엔진·플러그인 Error → 대개 **우리가 SDK 를 잘못된 상태·순서로 호출한 것**이라(이번 RPC 건이 정확히 그 사례) 조치는 **호출 시점 가드 추가**다. 코드를 여는 자리부터 다르다 |

→ **신설한다. 이름: `UnhandledEngineError`.**

- 이름 근거(`LogRules.md` **1.5** 「이름 짓기」): *"무엇이 일어났는지"* 를 적는다 — **우리가 계측하지 않은 곳에서 엔진 계층이 낸 오류**. `UnhandledException` 과 접두사를 맞춰 **같은 훅이 수집한 짝**임이 이름만으로 드러난다.
- `LogEvent` 멤버 수: **36개 → 37개**(실측 36개 확인 — `Application/Interfaces/ILogSink.cs`).
- **이름은 한 번 정하면 바꾸지 않는다**(1.5). 구현 시 다른 이름을 쓰고 싶으면 **구현 전에** 사용자에게 확인한다.
- `key=value`: 기존 `Source=UnityLogHook` 을 그대로 쓴다. **`LogType=` 필드는 두지 않는다** — 키가 이미 둘을 가르고 있어 잉여 필드이고, 잉여 필드는 집계에서 아무 질문에도 답하지 않는다.
- 축: `Ops.Error`(운영). 축 A = Error(엔진이 오류로 보고했고 우리 쪽에 복구 경로가 없다) / 축 B = 운영(①·② 모두 예 — 플레이어 기기의 엔진·회선 상태에 좌우되고, 이 로그 말고는 기록이 없다).

### 3-8. 질문 5 — 스팸 위험

**위험은 실재한다.** 엔진 오류는 `Update` 안에서 나면 **매 프레임 반복**된다(이번 RPC 오류가 정확히 그런 종류다). 그리고 수집된 줄은 그때마다 **파일에 즉시 기록**된다(`RuntimeLogger` 의 `AutoFlush = true` — 91행). 즉 파일 비대화와 **프레임당 디스크 I/O** 가 동시에 온다.

**`LogRules.md` 1.14 금지 8 과의 관계:** 금지 8은 *"매 틱·매 프레임 **로깅** 금지 — 상태 **전이** 시점에만 남긴다"* 로, **우리가 로그를 작성할 때의 규칙**이다. 이 훅은 남의 출력을 **수집**하는 자리라 문자 그대로의 대상은 아니다. 그러나 **결과("스팸은 정작 필요한 줄을 묻어 버린다")가 완전히 동일**하므로 취지를 따른다.

**방어 3으로는 못 막는다** — 직전 1건만 비교하므로 A→B→A→B 교대는 전부 통과한다.

**채택안: `condition` 단위 스로틀 (훅 내부)**

| 항목 | 방침 | 근거 |
|---|---|---|
| 판정 키 | **`condition` 문자열**(스택 제외) | 같은 자리에서 반복되는 엔진 오류는 `condition` 이 동일하다. `stackTrace` 는 여러 줄이라 비교 비용이 크고, 같은 사건인데도 프레임마다 달라질 여지가 있다 |
| 통과 규칙 | 같은 `condition` 은 **1초에 1건**만 통과 | 상태 전이는 놓치지 않으면서 프레임 단위 반복은 걷어낸다 |
| 억제분 표기 | 다음 통과 줄에 **`Suppressed=n`** 을 함께 남긴다 | 억제했다는 사실 자체가 사라지면 "몇 번 났는가"를 영원히 알 수 없다. `n` 은 집계 가능한 값이라 **1.4** 의 `key=value` 규정에 부합한다 |
| 자료구조 | `Dictionary<string, …>` + **상한 32개.** 넘으면 통째로 비우고 다시 시작 | 상한이 없으면 **매번 다른 문자열**(좌표·Id 가 섞인 메시지)이 들어올 때 딕셔너리가 무한히 커져 로그 시스템이 메모리 누수의 원인이 된다. "로그 때문에 게임이 멈추면 본말전도"(**1.8**) |
| 방어 3 유지 여부 | **그대로 둔다** | 목적이 다르다 — 방어 3은 **되먹임** 차단용, 스로틀은 **스팸** 억제용이다 |
| 적용 범위 | **훅이 수집한 줄에만** 적용 | 우리 코드가 직접 남기는 로그는 금지 8 이 이미 규율한다. 여기서 함께 억제하면 규칙이 두 곳으로 갈린다 |

> **주의:** 이 스로틀은 로그 시스템 본체 코드이므로 `[Conditional]` 을 붙이지 않는다. 릴리스에서 사라지면 정작 스팸이 문제되는 환경에서 억제가 없어진다.

---

## 4. A — `NetworkCombatController.Update` 가드 누락

### 4-1. 증상

게임 종료 후 [로비로] 버튼 → `Rpc methods can only be invoked after starting the NetworkManager!` (실기 **2회 재현**).

### 4-2. 원인 (실측)

```csharp
// NetworkCombatController.cs 288~308행
private void Update()
{
    if (!IsServer) return;          // ← 가드가 이것뿐 (291행)
    …
    TickCombat(realElapsed);        // 307행
}
```

- `NetworkGameManager.BackToLobby()`(**909~929행**)가 순서대로 ① 콜백 해제 ② Heartbeat 중지 ③ Lobby 퇴장 ④ **`ShutdownNetworkManager()`**(924행 → 889~896행에서 `NetworkManager.Shutdown()`) ⑤ 씬 전환 이벤트 발행을 수행한다.
- **④ 와 ⑤ 사이에 `Update` 가 한 번 더 돌면** `IsServer` 는 아직 참이라 `TickCombat` 이 실행되고, 그 끝에서 RPC 가 나간다.

**왜 하필 게임 종료 직후인가:** `TickCombat` 끝부분(**540~555행**)은 *"이번 틱에 타겟을 못 찾은 유닛"* 을 정리하며 `StopCombatClientRpc(id)`(554행)를 쏜다. 게임이 끝나면 유닛들이 일제히 타겟을 잃어 `toRemove` 가 **한꺼번에** 찬다.

### 4-3. 진입 경로 확인 (계획서가 확인하기로 한 항목)

| 확인 항목 | 결과 |
|---|---|
| `TickCombat` 을 부르는 다른 자리가 있는가 | **없다.** 파일 전체에서 `TickCombat(` **호출은 307행 1곳**뿐이다(나머지 매칭은 전부 주석) |
| `Update` 의 다른 진입 경로가 있는가 | **없다.** `Update` 는 Unity 가 부르는 자리 하나뿐이고, 외부에서 이 컨트롤러의 틱을 부르는 코드도 없다(`GameBootstrapper` 는 참조만 보관 — 148~149행) |
| 가드를 `Update` 에 둘 것인가 `TickCombat` 에 둘 것인가 | **`Update` 에 둔다.** 호출부가 1곳뿐이라 효과가 같고, **기존 `IsServer` 가드와 같은 자리에 모아 두는 편**이 "이 틱은 언제 도는가"를 한 줄에서 읽게 한다. `TickCombat` 안에 넣으면 판정이 두 메서드에 흩어진다 |

### 4-4. 채택안 — `IsSpawned` 가드 추가

`IsServer` 는 *"내가 서버 역할인가"* 이지 *"이 오브젝트가 아직 살아 있는가"* 가 아니다. 후자는 `IsSpawned` 다.

```
Update() 첫 줄을  →  if (!IsSpawned || !IsServer) return;
```

**순서(`IsSpawned` 를 앞에)의 근거:** `NetworkUnit.cs` **290행** 주석이 그대로 적고 있다 — *"`IsSpawned` 먼저 검사(`||` 단락 평가) — 싱글플레이(미스폰)에서는 `IsServer` 를 건드리지 않고 반환."*

**이 프로젝트의 선례 (실측):**

| 위치 | 코드 |
|---|---|
| `Infrastructure/Network/NetworkUnit.cs` **291행** | `if (!IsSpawned || IsServer) return;` |
| **같은 파일** `NetworkCombatController.cs` **886행** | `if (netObj != null && netObj.IsSpawned)` — Despawn 전에 생존을 먼저 본다 |
| `Infrastructure/Network/NetworkGameEndController.cs` **457행** | `if (netObj != null && netObj.IsSpawned == true && …)` |
| `Infrastructure/Factories/UnitFactory.cs` **533행** | `if (networkObject != null && networkObject.IsSpawned)` |

→ **새 패턴이 아니라 이 코드베이스가 이미 네 곳에서 쓰고 있는 관용구다.**

### 4-5. 남는 불확실성 (규칙 10 — 과대 표기 금지)

**"이 가드가 창을 완전히 닫는다"고 단정하지 않는다.** 근거를 확인할 수 없기 때문이다.

- `NetworkManager.Shutdown()` 이후 **`IsServer` 와 `IsSpawned` 중 어느 값이 먼저 내려가는지**는 NGO 내부 구현이고, **이 세션에는 NGO 패키지 소스가 없다**(`Library/PackageCache` 부재 — 실측).
- 관측 사실로 좁힐 수 있는 것: 오류 문구가 *"NetworkManager 를 시작한 뒤에만 RPC 를 호출할 수 있다"* 이므로 **RPC 발신 시점에 NGO 는 이미 정지 상태였고, 그런데도 `Update` 가 돌아 `IsServer` 를 통과했다.**
- 따라서 두 갈래다 — **(가)** 그 시점에 `IsSpawned` 가 이미 거짓이면 이 가드가 막는다. **(나)** 아직 참이면 못 막는다.
- **(가)가 유력한 이유:** `Shutdown()` 은 스폰된 오브젝트의 디스폰을 동반하고, 실제 로그에서도 Shutdown 직후 **8ms 안에** 세 컨트롤러의 「디스폰」 로그가 찍힌다(`RuntimeLog.txt` 311~315행: 22:38:55.835 Shutdown → .843/.845/.845 디스폰). 즉 디스폰은 **같은 틱 부근에서 즉시** 일어난다.
- **결론:** 가드는 **비용이 거의 0이고 되돌리기 쉬운 최소 변경**이며 선례도 확립돼 있으므로 먼저 적용하되, **실기 재현으로 확인한다**(§9). (나)로 판명되면 그때 종료 이벤트 구독(§10)으로 승격한다.

---

## 5. C — 연구 흐름에 로그가 없다

### 5-1. 무로그 구간 (실측)

`Infrastructure/Network/NetworkUpgradeController.cs`(392행)의 `GameLog` 호출은 **3개뿐**이고 **전부 실패·초기화 경로**다 — 71행(스폰 시 서비스 부재) · 84행(스폰 흔적) · 386행(착수 실패 알림 수신).

| 흐름 | 코드 위치 | 로그 |
|---|---|:-:|
| 클라 [연구] → `RequestResearchServerRpc` | 184행 | **없음** |
| 서버 `TryStartResearch()` 성공 | 224~235행 | **없음** |
| 서버 → 요청 클라 `ResearchStartedClientRpc` | 333행 | **없음** |
| **서버 완료 훅 → 브로드캐스트** | **279~282행** | **없음** ← 직전 커밋 `da5eeaab` 이 고친 버그의 지점 |
| 클라 레벨 반영 | **295~315행** | **없음** |

**대조:** `NetworkProductionController.cs` **285~286행**은 생산 완료를 `서버 유닛 생산 완료 | UnitId=…, UnitType=…, Team=…, Pos=…` 로 남긴다. 실제 로그 파일에도 그 줄이 다수 남아 있다(예: `RuntimeLog.txt` 300~302행). **연구만 사각지대다.**

### 5-2. 추가할 지점과 판정 — 축 B 2문을 실제로 물었다

`LogRules.md` **1.2** 축 B 의 두 질문을 각 지점에 그대로 적용했다. **결과가 전부 `개발`로 나오지 않았다** — 한 곳이 `운영`이다.

| # | 지점 | 축 A | 축 B ① 플레이어 기기에서만? | 축 B ② 다른 기록으로 대체 불가? | 판정 | 키 |
|:-:|---|:-:|---|---|:-:|---|
| 1 | 착수 성공 (`RequestResearchServerRpc` 235행 직전) | Info | **아니오** — 에디터 2인 구성으로 그대로 재현된다 | (①이 아니오라 불필요) | **개발** | 없음 |
| 2 | **완료 브로드캐스트** (`OnResearchCompletedOnServer` 279~282행) | Info | **아니오** — 동일 | — | **개발** | 없음 |
| 3 | 클라 레벨 반영 성공 (`ResearchLevelClientRpc` 303~308행 분기) | Info | **아니오** — 동일 | — | **개발** | 없음 |
| **3-b** | **같은 자리의 `else` 분기**(`upgrade == null` — 315행 앞, 309~314행) | **Warn** | **예** — 이 분기에 들어가는 조건이 **스폰 레이스**이고, 파일 주석(106~115행)이 *"회선 상태에 좌우돼 플레이어 기기에서만 어긋난다"* 고 확정해 두었다 | **예** — 레벨이 반영되지 않았는데 UI 는 완료로 보이므로 **화면상 정상과 구분되지 않는다** | **운영** | **`ClientRpcGameServicesMissing`**(재사용) |

**#1~#3 판정 선례:** `.claude/agent-memory/game-programmer/logging.md` 판정표의 *"스폰/디스폰/구독완료/RPC 수신 덤프/성공 통보 → Info / 개발"* 행과 같다.

**#3-b 가 신설 키가 아닌 이유:** 같은 메모리 표에 *"ClientRpc 안에서 `_services` 가 null … `ClientRpcGameServicesMissing`"* 이 이미 있고, **사건이 같으면 같은 키**를 쓴다. `LogRules.md` **1.5** 신설 기준(집계 분리 필요 + 조치 상이)을 **충족하지 못한다** — 조치가 동일하다(스폰 순서·조합 루트 등록 시점 점검).
**#3-b 축 A 가 Error 가 아닌 이유:** 바로 아래 313행이 `OnUpgradeChanged` 를 직접 발행해 **UI 가 갇히지 않도록 복구**한다. `LogRules.md` **1.2** 축 A 는 *"복구되었나?"* 하나만 묻는다 → 복구됨 → Warn.

> **#3-b 는 사용자 승인 대상으로 따로 표시한다.** 원 요청은 「3지점」이었고 이것은 **같은 지점의 반대 분기**다. 빼도 #1~#3 은 그대로 성립한다.

### 5-3. 로그 배치 위치 — 결정과 근거

**#3 · #3-b 는 `if (IsServer) return;`(297행) 가드 *뒤*에 둔다.**

- 297행 **앞**에 두면 host 에서도 찍힌다. 그런데 host 는 **#2(브로드캐스트 발신)를 같은 프로세스에서 이미 남긴 상태**라, 같은 사건이 한 파일에 두 줄로 남는다 → `LogRules.md` **1.14 금지 9** 위반.
- 가드 뒤에 두면 **순수 클라이언트에서만** 찍혀 중복이 원리적으로 불가능하다. 대신 **에디터가 client 인 회차에서만 파일에서 볼 수 있다**(§9 한계).

### 5-4. `key=value` 표기 (구현 시 그대로 사용)

기준: `.claude/agent-memory/game-programmer/logging.md` 「`key=value` 표기 규약」의 확정 매핑.

| 키 | 값 | 신규 여부 |
|---|---|:-:|
| `Team=` | `Blue` / `Red` (`TeamId` enum 이름 그대로) | 기존 |
| `BuildingId=` | 정수 | 기존 |
| `ClientId=` | 정수(`senderClientId`) | 기존 |
| `Group=` | `HumanMelee` / `HumanRanged` / `HumanVehicle` / `SpiritFire` / `SpiritWater` / `SpiritEarth` / `TransAnimal` / `TransPlant` (`UpgradeGroup` enum 이름 그대로) | **신규** |
| `Stat=` | `Attack` / `Defense` / `MoveSpeed` / `Regen` (`UnitUpgradeStat` enum 이름 그대로) | **신규** |
| `Level=` | 정수(0~5) | **신규** |
| `TotalSeconds=` | **정수**(`Mathf.RoundToInt(total)`) | **신규** |

- **enum 은 `ToString()` 결과를 그대로 쓴다** — 값에 `, ` 가 들어가지 않고 PascalCase 라 **1.4** 의 집계 가능 조건을 만족한다.
- **`TotalSeconds=` 를 정수로 고정하는 이유:** `float` 를 그대로 넣으면 **문화권(culture)에 따라 소수 구분자가 `,` 가 되어** 값 표기가 환경마다 갈린다(**1.4** 「같은 키의 값 표기도 파일마다 바꾸지 않는다」). 초 단위 정수면 그 위험이 구조적으로 없다.
- **`Group=` 은 `Stat=Regen` 일 때도 그대로 남긴다.** 자연회복은 그룹 무관 트랙이지만(`UpgradeGroup.cs` 71~74행 `RegenCanonicalGroup`), 필드를 조건부로 빼면 **같은 로그가 두 가지 필드 구성으로 갈려 파싱이 어려워진다.**

### 5-5. 메시지 문구 (초안)

| # | 문구 | data |
|:-:|---|---|
| 1 | `서버: 연구 착수 성공` | `ClientId=`, `Team=`, `Group=`, `Stat=`, `BuildingId=`, `TotalSeconds=` |
| 2 | `서버: 연구 완료 — 레벨 브로드캐스트` | `Team=`, `Group=`, `Stat=`, `Level=` |
| 3 | `클라이언트: 강화 레벨 반영` | `Team=`, `Group=`, `Stat=`, `Level=` |
| 3-b | `클라이언트: 서비스를 얻지 못해 레벨 반영 없이 완료만 통지했다` | `Team=`, `Group=`, `Stat=`, `Level=` |

**자유 문장은 메시지 쪽에만 둔다**(1.4). 메시지에 값을 섞어 쓰지 않는다.

---

## 6. 근거 규칙 정리

| 규칙 | 이 계획서에서 쓰인 곳 |
|---|---|
| `LogRules.md` **1.2** (두 축) | §5-2 — 착수·완료·반영 4건의 축 A/축 B 판정. §3-7 — 훅 Error 의 축 판정 |
| `LogRules.md` **1.5** (이벤트 키 신설 기준) | §3-7 — `UnhandledEngineError` **신설**(기준 2개 충족). §5-2 — `ClientRpcGameServicesMissing` **재사용**(기준 미충족) |
| `LogRules.md` **1.9** (전역 미처리 예외 수집 · 재진입 가드 필수) | §3 전체 — 수집 범위 확대와 되먹임 방어. *"이 가드가 없으면 로그 시스템이 크래시의 **원인**이 된다"* |
| `LogRules.md` **1.14 금지 8** (매 틱 로깅 금지) | §3-8 — 훅 스팸 억제의 근거(직접 대상은 아니나 취지 적용) |
| `LogRules.md` **1.14 금지 9** (같은 사건 두 곳 로깅 금지) | §3-4 — 직접 호출 경로의 2줄 메아리. §5-3 — `IsServer` 가드 뒤에 로그를 두는 이유 |
| `LogRules.md` **1.4 / 1.8 / 1.11 / 1.13** | §5-4 표기 · §3-5 sink 예외 정책 · §3-4 임시 로그 직접 호출 허용 · §3-2 이관 완료 사실 |
| `GameSystemRules_Units.md` **29·34·40** / `GameSystemRules_Buildings.md` **방어 타워 시스템 규칙 9** / `GameSystemRules_Upgrade.md` **규칙 7** | §2 · §4 — 멀티 서버 틱 진입점과 서버 권위 |
| `GameSystemRules_Upgrade.md` **규칙 8 · 9** | §2 · §5 — 연구소 운영과 완료/진행 전파 구분 |

---

## 7. 파일별 변경 계획

**구현 순서 제안:** **A → C → B**. A·C 는 국소 변경이라 먼저 넣어 컴파일을 확정하고, 되먹임 위험이 있는 B 를 마지막에 단독으로 넣어야 문제가 생겼을 때 원인이 섞이지 않는다.

| # | 파일 | 변경 내용 | 건 |
|:-:|---|---|:-:|
| 1 | `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | `Update`(291행) 가드를 `if (!IsSpawned || !IsServer) return;` 로 확장 + 이유 주석 | A |
| 2 | `Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs` | 로그 **4줄 추가**(개발 3 + 운영 1) — 235행 직전 · 279~282행 · 303~308행 분기 · 309~314행 분기 | C |
| 3 | `Assets/_Project/Scripts/Application/Interfaces/ILogSink.cs` | `LogEvent` 에 **`UnhandledEngineError` 1개 신설**(36 → 37) + 부여 근거 XML 주석 | B |
| 4 | `Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs` | 정적 플래그 `IsEmittingToConsole` 신설. `Log()` 의 콘솔 출력 `switch`(165~176행)를 `try` / `finally` 로 감싸 세우고 내린다 | B |
| 5 | `Assets/_Project/Scripts/Infrastructure/Debug/LogSessionOwner.cs` | ① 방어 1(314행) 조건을 `Exception` **또는** `Error` 로 확장 ② 방어 1-b(`IsEmittingToConsole`) 추가 ③ `type` 별 키 분기(`Exception` → `UnhandledException` / `Error` → `UnhandledEngineError`) ④ `condition` 단위 스로틀 + `Suppressed=` ⑤ **311~313행 주석 갱신**(이중 집계 근거가 소멸한 경위) | B |

**주석 작성 지침(CLAUDE.md 규칙 8):** 4·5번 파일의 변경에는 **"왜 이 방어가 필요한가"** 를 초급 개발자가 읽어도 이해할 수 있게 남긴다. 특히 5번 ⑤는 **"예전 판단이 틀린 것이 아니라 전제가 바뀌었다"** 는 점이 드러나야 한다 — 그렇지 않으면 다음 사람이 근거 없이 되돌린다.

**문서 반영은 이번 범위가 아니다.** 이 변경이 확정되면 `LogRules.md` **1.9 · 1.13** 의 *"`LogType.Exception` 만 수집한다"* 서술과 `LogEvent` 멤버 수(36)가 사실과 달라진다. **WORKFLOW.md [11]** 단계에서 함께 처리한다.

---

## 8. 위험 요소

### 8-1. 최우선 — B 의 되먹임 (발생하면 게임이 죽는다)

| 위험 | 대비 | 남는 불확실성 |
|---|---|---|
| 훅 → `GameLog` → sink → `Debug.LogError` → 훅 … 무한 고리 | 방어 4겹(§3-5). 주 경로는 **방어 2 로 이미 차단됨을 코드 경로로 확인**(§3-3) | Unity 의 훅 발화가 동기인지 **확정 불가**(엔진 소스 부재). 방어 3이 그 경우를 위해 이미 존재 |
| `RuntimeLogger` **직접 호출**(임시 로그) 경로가 `IsEmitting` 을 우회 | 방어 1-b 신설(§3-5 채택안) | 현재 호출처 **0건**이라 실기로는 재현되지 않는다 → **정적 검토로만 확인 가능** |
| 플래그가 내려가지 않아 **이후 모든 수집이 영구 차단** | `try` / `finally` 필수(§3-5). `GameLog.Emit` 519~524행과 동일 패턴 | — |

**중단 기준:** 구현 후 에디터 진입 시 콘솔이 폭주하거나 프레임이 눈에 띄게 떨어지면 **즉시 방어 1을 원래 조건으로 되돌린다**(한 줄 복원).

### 8-2. 그다음 — B 의 스팸

- 엔진 오류가 `Update` 안에서 나면 매 프레임 반복 → 파일 비대화 + `AutoFlush` 로 인한 **프레임당 디스크 I/O**.
- 대비: §3-8 스로틀. **딕셔너리 상한 32개**를 반드시 둔다(없으면 로그 시스템이 메모리 누수원이 된다).
- 남는 한계: 스로틀 창(1초) 안에서 **서로 다른** 오류가 대량으로 오면 억제되지 않는다. 그때는 원인이 되는 코드를 고치는 것이 정답이라 여기서 더 조이지 않는다.

### 8-3. A 의 잔여 경로 (이번 가드로 덮이지 않는다 — 실측)

`Update` 가드는 **`Update` → `TickCombat` 경로만** 막는다. 그런데 이 컨트롤러에는 **`Update` 를 거치지 않고 RPC 를 쏘는 경로가 남아 있다.**

```
ExecuteAttack(605행) → StartCoroutine(DelayedAttackDamage)(634행)
  → 대기(hitFrameTime) → combat.ApplyAttackDamage(589행)
  → 대상 사망 시 GameEvents.OnUnitDied 발행 → OnUnitDied(828행) → EntityDiedClientRpc(852행)
```

- 코루틴은 **Shutdown 이전에 시작된 것이 Shutdown 이후에 깨어날 수 있다.** 대기 시간이 타격 프레임(1초 미만)이라 창은 좁지만 **0은 아니다.**
- `OnUnitDied`(832행)의 가드도 `if (!IsServer) return;` 하나뿐이라 `Update` 와 같은 성질의 구멍이다.
- **이번 범위에서는 고치지 않는다**(CLAUDE.md 규칙 6). §10 에 후속 후보로 남긴다. 실기에서 A 를 고친 뒤에도 같은 오류가 남으면 **여기를 먼저 의심한다.**

### 8-4. C 의 로그 볼륨

- 연구는 최소 15초 이상 걸리는 저빈도 이벤트라 **금지 8 과 충돌하지 않는다**(매 틱 로그가 아니다).
- #1~#3 은 `개발` 이라 릴리스에서 호출과 문자열 보간까지 통째로 사라진다(**1.7**).
- **주의:** `[Conditional]` 이 붙는 `Dev.*` 를 **람다에 넣지 않는다.** 넣어야 하면 반드시 **블록 본문 `{ }`** 로 쓴다(**1.7** 「람다에 넣으면 스트리핑이 무효가 된다」). 이번 4자리는 전부 문(statement) 자리라 해당 없음.

---

## 9. 검증 방법

### 9-1. 빌드 없이 에디터에서 검증할 수 있다 — 근거

| 건 | 근거 |
|:-:|---|
| **A** | `Update` 가 `if (!IsServer) return;` 이라 **서버(=host)에서만** 돈다. 에디터가 host 인 회차면 에디터에서 그대로 재현·확인된다 |
| **B** | 파일 기록은 **에디터 전용**이다 — `FileSink` 의 동작 전체가 `#if UNITY_EDITOR` 안에 있고(`FileSink.cs` 121·153행 등), `RuntimeLogger` 의 파일 쓰기도 마찬가지다(`RuntimeLogger.cs` 44~49 · 147~161행). 즉 **확인할 수 있는 곳이 애초에 에디터뿐**이다 |
| **C** | #1(착수 성공)·#2(완료 브로드캐스트)는 **서버에서** 실행되므로 에디터가 host 인 회차에 파일에 남는다 |

**기기 빌드는 낡았지만(386건 이관 미포함) 문제가 되지 않는다** — 기기가 client 인 회차에서는 위 세 지점이 **기기에서 아예 돌지 않기 때문**이다.

### 9-2. 확인 항목

| # | 항목 | 방법 | 기대 결과 |
|:-:|---|---|---|
| 1 | B — Error 수집 | 게임 종료 → [로비로] (A 수정 **전** 상태에서 B 만 켜고 1회) | 로그 파일에 `[ERROR] … Event=UnhandledEngineError` 줄이 남는다 |
| 2 | B — 되먹임 없음 | 위 1 실행 중 콘솔·프레임 관찰 | 같은 줄이 폭주하지 않고, 에디터가 멈추지 않는다 |
| 3 | B — 스팸 억제 | 위 1 의 로그 파일 확인 | 동일 오류가 초당 1줄 이하로 남고 `Suppressed=` 가 함께 찍힌다 |
| 4 | **A — 가드 동작** | A 수정 **후** 게임 종료 → [로비로] | RPC 오류가 **더 이상 나지 않는다.** (항목 1에서 확인한 수집 경로 덕분에, 나면 파일에 남는다) |
| 5 | C — 착수·완료 | 연구 1건을 끝까지 진행 | `서버: 연구 착수 성공` · `서버: 연구 완료 — 레벨 브로드캐스트` 가 파일에 남는다 |
| 6 | C — 클라 반영 | **에디터가 client 인 회차**에서 상대가 연구를 완료 | `클라이언트: 강화 레벨 반영` 이 파일에 남는다 |

> **항목 1과 4의 순서가 중요하다.** B 를 먼저 켜야 A 의 수정 여부를 **로그로** 판정할 수 있다. A 를 먼저 고치면 오류가 사라져 B 의 수집 동작을 확인할 소재가 없어진다.

### 9-3. 한계 (과대 표기 금지 — CLAUDE.md 규칙 10)

1. **호스트가 랜덤으로 정해진다.** 이 프로젝트의 테스트 구성은 「에디터 1 + 실기기 빌드 1」이고 어느 쪽이 host 가 될지 정해져 있지 않다(`LogRules.md` 1.10 · `logging.md` 확정 사실). **기기가 host 인 회차에서는 A 를 검증할 수 없다.** 실제로 이번 로그 2회차 모두 에디터가 host 였다(`Role=Host` — `RuntimeLog.txt` 44·362행).
2. **C 항목 6은 반대 회차가 필요하다.** #3 로그는 `IsServer` 가드 뒤에 있어(§5-3) **에디터가 client 인 회차**에서만 파일에 남는다.
3. **A 의 원인인 스폰 레이스 계열 타이밍은 강제 재현이 어렵다.** 이번 로그에는 `NetworkControllerSpawnedWithoutGameServices` 경고가 **0건**이다(= 레이스 미발생). 항목 4가 1회 통과했다고 해서 "창이 닫혔다"고 단정하지 않는다 — §4-5 참조.
4. **C #3-b(운영 로그)는 스폰 레이스 상황에서만 찍힌다.** 정상 회차에서는 **한 줄도 안 나오는 것이 정상**이며, 그것이 검증 실패를 뜻하지 않는다.
5. **B 의 「직접 호출 우회」는 실기로 재현할 수 없다**(호출처 0건). 정적 검토로만 확인한다.

---

## 10. 범위 밖 (이번에 하지 않는다 — CLAUDE.md 규칙 6)

| 항목 | 왜 미루는가 | 재개 조건 |
|---|---|---|
| **게임 종료 시 전투 루프 정지** — `NetworkCombatController` 에 게임 종료 구독이 **0건**이다(실측: `OnGameEnd` 계열 grep 0). 그래서 승패가 갈린 뒤에도 전투가 계속 돈다 — 이번 로그에서 **약 20초**(22:38:35 종료 감지 → 22:38:55 Shutdown) | 이벤트 구독·해제와 상태 초기화가 얽힌 **구조 변경**이라 이번 3건과 성격이 다르다. 그리고 `IsSpawned` 가드는 **연결 끊김·강제 종료를 포함한 모든 종료 경로**를 한 줄로 덮는 반면, 종료 구독은 **정상 종료 경로만** 덮는다 — 즉 가드가 더 넓은 방어다 | A 검증에서 §4-5 (나)로 판명되면 즉시 |
| **`Update` 밖의 RPC 경로 가드**(§8-3 — 코루틴 → `OnUnitDied` → `EntityDiedClientRpc`) | 요청 범위는 `Update` 가드다. 창이 좁고(1초 미만) 이번 실기에서 이 경로의 오류는 관측되지 않았다 | A 수정 후에도 같은 오류가 남으면 |
| **기기 빌드 갱신** | 386건 이관이 반영되지 않은 낡은 빌드이지만, §9-1 근거대로 **이번 검증 3건은 기기에서 돌지 않는다** | 실기기 전용 문제를 추적할 때 |
| **`NetworkProductionController.cs` 286행의 형식 위반** — `Pos={unit.Position}` 가 `Pos=(4, 15)` 를 만든다. **값 안에 구분자 `, ` 가 들어가 파싱이 그 자리에서 쪼개진다**(`LogRules.md` **1.4** 2026-08-18 추가 항목 위반). 실제 로그 파일에서 확인됨(`RuntimeLog.txt` 300~302행) | 이번 작업 대상 파일이 아니다. `개발` 로그라 서버 집계에는 오르지 않는다 | 별도 작업. **단, C 에서 같은 실수를 되풀이하지 않도록 §5-4 에 표기 규약을 못 박았다** |
| **`LogRules.md` 1.9 · 1.13 서술 갱신**(수집 범위 · `LogEvent` 멤버 수 36 → 37) | 구현이 확정되기 전에 규칙 문서를 고치면 **문서가 코드보다 앞서 사실이 아닌 상태**가 된다 | WORKFLOW.md **[11]** 단계 |
| **`FileSink.EditorLogsRootRelativeToAssets` 의 경로 문자열 복제 해소** | `LogRules.md` 1.13 이 이미 「범위 밖으로 미룬 항목」으로 기록해 둔 건이다 | 별도 작업 |

---

## 승인 요청 사항 (구현 착수 전 확인이 필요한 3건)

1. **`LogEvent.UnhandledEngineError` 이름** — 한 번 정하면 바꾸지 않는다(**1.5**). 이 이름으로 확정해도 되는가.
2. **C #3-b(운영 로그 1줄) 포함 여부** — 원 요청은 3지점이었고 이것은 같은 지점의 반대 분기다. 빼도 나머지는 성립한다.
3. **검증 순서(§9-2)** — B 를 먼저 켠 뒤 A 를 고치는 2단계 진행에 동의하는가. 한 번에 다 넣으면 A 의 수정 효과를 로그로 판정할 수 없다.

---

# 11. 구현 결과 (2026-08-19 추가 · 커밋 `cc864054`)

> **아래는 계획이 아니라 실제로 벌어진 일의 기록이다.** 위 §1~§10 은 착수 시점의 계획이므로 원문을 그대로 둔다.
> 아래 내용은 **코드 재실측 + 실기 로그 `_Logs/_editor/2026-08-19/RuntimeLog.txt`(704줄) 판독**으로 확인했다(CLAUDE.md 규칙 10).

## 11-1. 3건 모두 구현 완료

| 건 | 결과 | 실측 확인 |
|:-:|---|---|
| **B** | **구현 완료** | `LogSessionOwner.cs` **389행** `if (type != LogType.Exception && type != LogType.Error) return;` · **400행** `if (RuntimeLogger.IsEmittingToConsole) return;` · **432~435행** `type` 별 키 분기 · **457행** `Source=UnityLogHook, Suppressed={suppressedCount}` · 스로틀 표(**145행**)와 `Stopwatch`(**152행**)와 상한(**519행**). `RuntimeLogger.cs` **79행** `IsEmittingToConsole` 공개 프로퍼티 · **202행** 콘솔 출력 구간만 감싼다. `ILogSink.cs` **108행** `UnhandledEngineError` |
| **A** | **구현 완료** | `NetworkCombatController.cs` **310행** `if (!IsSpawned \|\| !IsServer) return;` |
| **C** | **구현 완료** | `NetworkUpgradeController.cs` — **246행**(착수 성공 `Dev`) · **305행**(완료 브로드캐스트 `Dev`) · **346행**(클라 반영 `Dev`) · **369행**(서비스 부재 `Ops` — 기존 키 `ClientRpcGameServicesMissing` 재사용). **326행 주석**에 「`IsServer` 가드 뒤에 두는 이유」가 함께 적혀 있다 |
| — | **`key=value` 구분자 위반 수정** | `NetworkProductionController.cs` **293행** — ~~`Pos={unit.Position}`~~ → **`Q={unit.Position.Q}, R={unit.Position.R}`**. §10 에서 "범위 밖"으로 미뤄 두었던 항목인데, **1.4 를 우리가 어기고 있는 자리**라 이번에 함께 고쳤다(290행 주석에 근거 기재) |

**`LogEvent` 실측 = 37개** (`ILogSink.cs` 파싱 결과 `Unknown`(0) 포함). 계획의 「36 → 37」과 일치한다.

## 11-2. 계획과 달라진 점 — **1건**

| 항목 | 계획(§3-8) | 실제 |
|---|---|---|
| `Suppressed=` 를 붙이는 조건 | *"다음 통과 줄에 `Suppressed=n` 을 함께 남긴다"* — 억제분이 있을 때만 붙는 것으로 읽힌다 | **처음 통과하는 줄에도 `Suppressed=0` 을 항상 붙인다.** 필드를 조건부로 빼면 **같은 로그가 두 가지 필드 구성으로 갈려 파싱이 어려워지기 때문**이다. §5-4 가 `Group=` 에 대해 이미 같은 판단을 내려 두었고(*"필드를 조건부로 빼면…"*), 그 판단을 훅 쪽에도 똑같이 적용한 것이다 |

**그 밖에는 계획대로다** — 방어 4겹 구성 · `Assert` 제외 · 키 신설과 이름 · 스로틀 1초 / 상한 32 / `Stopwatch` · `try`/`finally` · 가드 위치(`Update`) · C 4지점과 `IsServer` 가드 뒤 배치.

## 11-3. 실기 검증 결과 (2026-08-19) — **A ✅ / C ✅ / B ⚠️ 미검증**

근거: `_Logs/_editor/2026-08-19/RuntimeLog.txt` (704줄).

| # | 항목 | 결과 · 근거 |
|:-:|---|---|
| **A** | RPC 에러(§9-2 항목 4) | ✅ **해결.** Shutdown(**13:34:01.467** · 692행) 이후 `StopCombatClientRpc` **0건**. 에디터가 host 인 회차라(`Role=Host` **4건**, `Role=client` 0건) 검증 조건 성립 |
| **C** | 연구 흐름(§9-2 항목 5) | ✅ **정상.** `서버: 연구 착수 성공` **6건**(397·408·420·518·525·533행) ↔ `서버: 연구 완료 — 레벨 브로드캐스트` **6건**(445·455·466·592·596·603행) **1:1**. `Level=1` → `Level=2` 상승 확인 |
| **B** | 엔진 오류 수집(§9-2 항목 1·3) | ⚠️ **미검증.** `[ERROR]` **0건** · `UnhandledEngineError` **0건** · `Suppressed=` **0건** — **이번 세션에 엔진 오류가 나지 않아 잡을 것이 없었다.** 훅이 동작한다는 증거는 아직 없다 |
| — | 되먹임·폭주(§9-2 항목 2) | ✅ 없음. 704줄이 정상 형식으로 기록되었고 같은 줄의 폭주가 없다 |
| **C #3** | 클라 반영(§9-2 항목 6) | **해당 회차 없음.** `클라이언트: 강화 레벨 반영` 0건은 **정상**이다 — `if (IsServer) return;` 뒤에 있어 host 는 건너뛰고, 기기(순수 클라)는 빌드라 파일을 쓰지 않는다(§5-3 · §9-3 2번) |

## 11-4. ⚠️ 완료 옆에 붙는 단서 (규칙 10 — 과대 표기 금지)

1. **B 는 「동작 확인」이 아니라 「확인할 기회가 없었음」이다.** 오류가 나지 않아 잡을 것이 없었을 뿐, 훅이 동작한다는 증거가 아니다. §9-2 항목 1·3 은 **아직 미판정**이다.
2. **A 의 원인인 스폰 레이스 계열 타이밍은 이번에도 미발생이다** — `NetworkControllerSpawnedWithoutGameServices` **0건**. §4-5 가 적은 대로 **"창이 닫혔다"고 단정하지 않는다.** 확인된 것은 **정상 경로에서 오류가 나지 않았다**는 사실까지다.
3. **`SetCameraStartPositionForTeam` 주석 처리분의 최종 삭제가 아직 남아 있다** (직전 task `04_02` §11-3 1번과 동일 건).
4. **기기 빌드가 낡았다** — 386건 이관과 이번 수정이 **미포함**이다. **기기가 host 인 회차에서는 A 가 동작하지 않는다**(§9-3 1번).
5. **범위 밖으로 남긴 2건은 그대로다** — §8-3 의 `OnUnitDied` → `EntityDiedClientRpc` **같은 성질의 가드 구멍** / §10 의 **`NetworkCombatController` 게임 종료 구독 부재**. 후자는 이번 로그에서도 확인된다 — **13:33:58.860 종료 감지 → 13:34:01.467 Shutdown, 약 3초간 전투 루프 지속**(2026-08-18 회차의 약 20초보다는 짧다).
6. **`Suppressed=` 는 발생 횟수의 하한이다** — 방어 ④가 먼저 버린 줄은 스로틀에 도달하지 않는다. 정확한 발생 횟수로 읽지 않는다.

## 11-5. 변경 파일 리스트업 (WORKFLOW.md [12])

> **git 명령을 쓰지 않았다**(CLAUDE.md 규칙 5). 아래는 §7 의 계획을 **코드 재실측으로 대조**한 결과다.

```
[수정]
- Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs    (A — Update 가드)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs   (C — 로그 4줄)
- Assets/_Project/Scripts/Application/Interfaces/ILogSink.cs                   (B — LogEvent 36→37)
- Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs                (B — IsEmittingToConsole)
- Assets/_Project/Scripts/Infrastructure/Debug/LogSessionOwner.cs              (B — 수집 범위·키 분기·스로틀)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs (§10 → Pos= 를 Q=/R= 로)

[변경 없음]
- 씬 · 프리팹 (Inspector 작업 없음 — WORKFLOW [5-2] 해당 없음)

[문서]
- Assets/_Project/Docs/LogRules.md                       (1.5 / 1.9 / 1.11 / 1.13 · 서두 개정 이력)
- Assets/_Project/Docs/_Tasks/2026-08-18/20_07_log-coverage-and-rpc-guard/Plan.md  (이 문서 §11 추가)
- Assets/_Project/Docs/_Tasks/2026-08-18/04_02_upgrade-subscription-fix/Plan.md    (§12 추가)
- Assets/_Project/Docs/PROJECT_STATUS.md · ROADMAP.md · WORK_HISTORY.md

[로그]
- Assets/_Project/Docs/_Logs/_editor/2026-08-19/RuntimeLog.txt  (실기 근거 — 704줄)
```
