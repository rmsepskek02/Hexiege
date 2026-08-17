# Plan — 씬과 무관하게 로그를 수집한다 (scene-independent logging)

작성일: 2026-08-17 · 작업 폴더: `_Tasks/2026-08-17/11_07_scene-independent-logging/`

---

## ⚠️ 최상단 고지 — 기존 로직 "제거"가 포함된다 (WORKFLOW.md [4] 기존 로직 제거 규칙)

이 계획에는 **기존 호출 한 곳을 없애는 변경**이 들어 있다. 규칙에 따라 최상단에 먼저 적는다.

| 대상 | 현행 | 이 계획 | 안전 근거 |
|---|---|---|---|
| `GameBootstrapper.cs` **486행** `ShutdownLogging();` (`OnDestroy` 안) | 씬이 언로드될 때 로그 파일 세션을 닫는다 | **호출을 없앤다.** 닫는 일은 앱/플레이모드 종료 시점으로 옮긴다 | 이 호출이 남아 있으면 Game 씬을 떠날 때 파일이 닫혀, **Login 에서 열어 둔 세션까지 끊긴다.** 이 계획의 목적 자체와 정면으로 충돌한다 (§4 참조) |

**검증 전까지는 삭제하지 않고 주석 처리(비활성화)한다.** 주석으로 남긴 코드의 최종 삭제는
WORKFLOW.md 규정대로 **[6] 사용자 테스트 통과 후 · [7] 문서/메모리 업데이트 전**에 수행한다.

---

## 0. 이 문서의 상태

- **이 Plan 은 코드가 한 줄도 바뀌지 않은 상태에서 작성되었다.** 아래 모든 실측값은 현행 코드를 읽어 확인한 것이다.
- 구현은 **`game-programmer` 에이전트**가 수행한다. 문서 담당은 코드를 작성하지 않는다.
- **사용자 승인 후에만 구현을 시작한다** (WORKFLOW.md [4]).
- 이번 사이클에서 `Research.md` 는 **사용자 지시로 생략**했다. 조사 결과는 이 문서 §2·§6 에 함께 담았다.

---

## 1. 무엇을 왜 하는가 (자연어 설명 — CLAUDE.md 규칙 13)

지금 이 게임은 **게임 화면(전투)에서 벌어진 일만 기록 파일에 남긴다.**
로그인 화면과 로비 화면에서 무슨 일이 있었는지는 **한 글자도 남지 않는다.**

문제가 되는 이유는 이렇다. 출시 후 실제 플레이어에게 가장 자주 생기는 사고는
"전투 중 버그"가 아니라 **"로그인이 안 된다", "상대를 못 찾는다"** 쪽이다.
그런데 정확히 그 구간의 기록이 비어 있어서, 그런 신고가 들어와도
개발자는 **무엇을 보고 원인을 찾아야 할지 알 수 없다.**

사용자가 밝힌 목적을 그대로 옮기면 이렇다.

> **"로그인과 로비도 라이브에서 문제가 생겼을때 로그를 수집해야해. 씬과 구분없이 구현하는게 목적이야."**

원인은 단순하다. **기록을 시작하는 스위치가 전투 화면에만 달려 있다.**
게임을 켜면 로그인 화면 → 로비 화면 → 전투 화면 순서로 넘어가는데,
기록 스위치는 마지막 전투 화면에 들어가서야 켜진다.
그 앞 두 화면에서 일어난 일은 이미 지나간 뒤라 파일에 담기지 않는다.

그리고 **스위치를 끄는 자리도 잘못되어 있다.** 지금은 "전투 화면을 벗어날 때" 꺼진다.
그래서 켜는 스위치를 로그인 화면에 그냥 하나 더 달면, 로그인 화면을 벗어나는 순간
그 스위치가 파일을 닫아 버려서 **오히려 지금 남던 전투 기록까지 잃을 수 있다.**

그래서 이번 작업은 이렇게 바꾼다.

- **켜는 쪽은 여럿, 끄는 쪽은 하나로 뒤집는다.** 어느 화면에서 게임을 시작하든 **가장 먼저 뜨는 화면이 기록을 켠다.**
  이미 켜져 있으면 아무 일도 하지 않는다.
- **끄는 시점은 "화면을 벗어날 때"가 아니라 "앱을 끌 때" 하나로 모은다.**
- 결과적으로 **로그인 실패부터 게임 종료까지가 파일 하나에 한 흐름으로 이어진다.**

한 줄로 줄이면 — **로그인·로비에서 문제가 생겨도 지금은 기록이 남지 않는다. 어느 화면에서든 남게 바꾼다.**

---

## 2. 문제 — 실측 근거

### 2-1. 커밋된 로그 파일이 게임 씬부터 시작한다

사용자가 랜덤매칭 1회를 실기 테스트하고 커밋한 파일:
`Assets/_Project/Docs/_Logs/_editor/2026-08-17/RuntimeLog.txt` (**총 70행**)

```
1  === 에디터 상시 런타임 로그 ===
2  === 세션 시작: 2026-08-17 19:31:37 ===
3
4  [19:31:38.275] [INFO] [Network/NetworkCombatController] 네트워크 역할 확정 | Role=host
```

**헤더 2줄 + 빈 줄 다음, 첫 본문 줄이 곧바로 `NetworkCombatController` 다.**
`NetworkCombatController` 는 Game 씬에서 스폰되는 네트워크 오브젝트이므로,
이 파일은 **Game 씬 진입 시점부터 시작한다.** 로그인·로비 구간은 한 줄도 없다.

파일 전체에서 `LobbyManager` 가 나오는 유일한 줄은 **70행**이고, 이것도 게임이 끝난 뒤다.

```
70 [19:33:48.325] [INFO] [Network/LobbyManager] Lobby 삭제 완료 (Host 퇴장) | LobbyId=8d9e4818-...
```

즉 `LobbyManager` 의 로그조차 **Game 씬에 있는 동안 발생한 것만** 파일에 남았다.

### 2-2. 원인 — 스위치가 Game 씬에만 있다

| 사실 | 근거 (실측) |
|---|---|
| `InitializeLogging()` 은 `GameBootstrapper.Awake` 에서만 호출된다 | `Bootstrap/GameBootstrapper.cs` **468행** |
| `ShutdownLogging()` 은 `GameBootstrapper.OnDestroy` 에서 호출된다 | `Bootstrap/GameBootstrapper.cs` **486행** |
| `GameBootstrapper` 는 **Game.unity 에만** 배치되어 있다 | 씬 파일에서 `GameBootstrapper.cs` 의 GUID(`ac1e9aace5b88ad4585e560c54709adc`) 참조 수 — **Login 0 / Lobby 0 / Game 1** |
| `LoginBootstrapper` 는 **Login.unity 에만** 있고, 로그 배선이 전혀 없다 | GUID(`ee24f061cf972d74a8809dae3e357171`) 참조 수 — Login 1 / Lobby 0 / Game 0. `LoginBootstrapper.cs` 전체에 `GameLog`·`InitializeLogging`·`BeginSession` 호출 **0건** |
| **Lobby.unity 에는 부트스트래퍼가 아예 없다** | 위 두 GUID 모두 Lobby.unity 에서 **0건** |

> **부수 발견 — 코드 주석이 사실과 다르다.**
> `LoginBootstrapper.cs` **13행** 주석은 *"GameBootstrapper 는 **Lobby**/Game 씬에 존재"* 라고 적고 있으나,
> 위 실측대로 Lobby.unity 에는 없다. 이 주석은 이번 작업에서 함께 정정한다(§7).

따라서 Login·Lobby 씬의 `GameLog` 호출은 **sink 가 하나도 등록되지 않은 상태**에서 실행되어,
`LogRules.md` **1.8** 의 콘솔 폴백만 타고 **파일에는 남지 않는다.**

### 2-3. 영향 범위 — 이관까지 끝낸 로그가 파일에 못 닿는다

이미 `GameLog` 로 이관이 끝난 8파일 중, **로그인·로비 구간 4파일의 호출이 파일에 닿지 못한다.**
아래 건수는 각 파일의 `GameLog.Ops.` / `GameLog.Dev.` 호출을 실측한 값이다.

| 파일 | `GameLog` 호출 건수 | 파일 기록 |
|---|:-:|---|
| `Infrastructure/Auth/FirebaseAuthService.cs` | **20** | ❌ 전무 |
| `Application/UseCases/LoginUseCase.cs` | **18** | ❌ 전무 |
| `Infrastructure/Network/UnityServicesInitializer.cs` | **7** | ❌ 전무 |
| `Infrastructure/Network/LobbyManager.cs` | **28** | 일부만 (Game 씬 구간 — 위 70행이 그 예) |
| **합계** | **73** | |

`LogRules.md` **1.13** 이 기록한 **호출부 마스킹 15곳 중 14곳이 바로 이 4파일**이므로
(`FirebaseAuthService` 10 · `UnityServicesInitializer` 2 · `LoginUseCase` 2),
**마스킹이 실제로 동작하는지를 파일로 검증할 수단이 지금 없다.**

### 2-4. 규정 위반

`LogRules.md` **1.10** 은 *"에디터에서는 파일 기록을 항상 켠다. 플레이 모드에 들어가면 파일이 자동으로 쌓인다"* 고 규정한다.
실제 커버리지는 **Game 씬뿐**이므로 이 규정이 지켜지지 않고 있다.

또한 `LogRules.md` **1.9** 의 전역 미처리 예외 수집도 같은 뿌리의 문제를 갖는다 — §6-2 참조.

---

## 3. 근거 규칙

### 3-1. `GameSystemRules` — **해당 없음**

`Assets/_Project/Docs/GameSystemRules.md`(인덱스)를 읽고 확인했다.
등재된 12개 세부 규칙 파일은 맵 / 랜덤맵 / UI / 유닛 / 건물 / 스킬 / 업그레이드 / Canvas SortingOrder / 사운드 / AI(+시나리오 3종)이며,
**로그 인프라를 다루는 규칙은 존재하지 않는다.**

이번 작업은 게임 시스템이 아니라 **로그 인프라의 배선 위치**를 바꾸는 것이므로,
근거 규칙은 아래 `LogRules.md` 조항으로 대체한다.

### 3-2. `LogRules.md` — 근거 조항

| 조항 | 내용 | 이 작업과의 관계 |
|---|---|---|
| **1.8** sink 구조 | sink 등록은 **조합 루트가 수행**한다. sink 가 없으면 콘솔로 폴백한다 | 등록 주체를 늘리되 **조합 루트(부트스트래퍼)만 호출**하는 구조를 유지해야 한다. 지금 Login·Lobby 가 겪는 것이 바로 "콘솔 폴백만 타는" 상태다 |
| **1.9** 예외 처리 | `Application.logMessageReceived` 훅으로 **계측하지 않은 곳의 예외까지 수집**한다 | 이 훅도 `GameBootstrapper` 에만 걸려 있어 Login·Lobby 구간 미처리 예외가 수집되지 않는다. 같은 뿌리의 문제이므로 함께 옮긴다(§6-2) |
| **1.10** 파일 기록 — 에디터 상시 활성 | 세션을 **수동으로 열고 닫지 않는다.** 플레이 모드에 들어가면 파일이 자동으로 쌓인다. **일 단위 이어쓰기** | 이 작업이 정면으로 겨냥하는 조항이다. 「세션 소유권 문제도 구조적으로 없어진다」는 이 조항의 취지를 이번 변경이 실제로 성립시킨다 |
| **1.4** 형식 / 「`key=value` 가 곧 전송 데이터다」 | 운영 로그는 그대로 서버 전송용 구조화 데이터가 된다 | 로그인·매칭 실패가 라이브에서 가장 흔한 이슈이므로, 이 구간이 비면 서버 수집 설계의 절반이 빈다 |
| **1.14** 금지 사항 1 | 콘솔에만 남은 로그는 파일이 되지 않아 **Claude 가 읽을 수 없다** | 현재 로그인·로비 73건이 정확히 이 상태다 |

---

## 4. 채택안 — "여는 쪽은 여럿, 닫는 쪽은 하나"

| | 현재 | 변경 후 |
|---|---|---|
| **여는 시점** | `GameBootstrapper.Awake` 단독 | **가장 먼저 뜨는 부트스트래퍼가 연다.** 멱등(`EnsureInitialized()`) — 이미 열려 있으면 아무것도 하지 않는다 |
| **닫는 시점** | `GameBootstrapper.OnDestroy` (= 씬 언로드) | **`UnityEngine.Application.quitting`** — 앱/플레이모드 종료 시 1회 |
| **Lobby 씬** | 커버 안 됨 | Login 에서 연 세션이 **그대로 살아 있다.** 부트스트래퍼를 새로 만들지 않는다 |

### 왜 "닫는 시점을 옮기는 것"이 이 작업의 핵심인가

지금은 **씬이 언로드될 때 닫는다.** 여기서 여는 배선만 Login 에 추가하면,
Login → Lobby 전환 때 `LoginBootstrapper` 가 파괴되면서 세션이 닫혀
**오히려 지금 남던 Game 씬 로그까지 잃는다.** 여는 쪽만 손대는 것은 개선이 아니라 퇴행이다.

### 이 설계가 자동으로 얻는 것

- **`Game.unity` 를 직접 열고 실행해도** `GameBootstrapper` 가 초기화하므로 **현행 동작이 그대로 유지된다.**
  `UIManager` 가 가진 *"Lobby/Game 씬 직접 진입 시 `Instance = null`"* 약점을 로그에서는 반복하지 않는다.
- 씬을 오가도 **파일 하나에 이어진다** → 로그인 실패부터 게임 종료까지 한 흐름으로 읽힌다.
  `LogRules.md` **1.10** 의 「일 단위 이어쓰기」·「헤더가 실행 구분선」 규정과도 어긋나지 않는다
  (헤더는 **앱 실행 1회당 1개**가 되어 오히려 구분선으로서 더 정확해진다).

### ⚠️ 다만, 채택안을 그대로 옮기는 것만으로는 목적이 달성되지 않는다

`InitializeLogging()` 을 **"멱등하게 만든다"** 는 서술만으로는 사고를 막을 수 없다.
현행 코드를 읽어 확인한 결과, 상태가 **인스턴스에 묶여 있어** 멱등 가드가 씬 사이를 건너가지 못한다.
상세는 **§6-1** 에 적었고, 그 결론이 **§6-3 의 최종 선택(정적 소유자)** 을 결정한다.

---

## 5. 기각안

| # | 안 | 기각 사유 |
|:-:|---|---|
| **A** | **각 씬 부트스트래퍼가 각자 열고 닫는다** | ① **Lobby.unity 에 부트스트래퍼가 아예 없어 성립하지 않는다**(§2-2 실측). ② 씬 전환마다 세션이 끊겨 파일이 조각나고, 하루 한 파일에 헤더가 씬 전환 횟수만큼 찍힌다. ③ 여닫는 주체가 여럿이 되어 **2026-08-10 세션 소유권 사고의 구조가 그대로 재현**된다(§6-1) |
| **B** | **`[RuntimeInitializeOnLoadMethod]` 로 씬과 무관하게 자동 초기화** | 목적만 보면 가장 깔끔하지만 **조합 루트 원칙과 충돌한다** — `LogRules.md` **1.8** 이 「등록 주체 = `GameBootstrapper`(유일한 의존성 조합 루트)」로 못 박고 있고, 이 속성을 쓰면 **아무도 호출하지 않았는데 sink 가 등록되는** 경로가 생겨 "누가 조합했는가"를 코드에서 추적할 수 없게 된다. 실측: 현재 `Assets/_Project/Scripts/` 전체에 `[RuntimeInitializeOnLoadMethod]` 사용처 **0건** — 이 프로젝트에 없는 패턴을 로그 인프라가 처음 들여올 이유가 없다. **검토했으나 채택하지 않는다** |
| **C** | **`DontDestroyOnLoad` 싱글턴 오브젝트를 Login 씬에 배치** | `UIManager`·`AudioManager`·`ToastUI` 의 선례가 이미 있어 낯설지 않다. 그러나 **그 선례의 약점을 그대로 물려받는다** — `LoginBootstrapper.cs` 137~139행이 *"UIManager.Instance 가 null 입니다"* 경고를 두고 있는 것이 그 증거다. **Game 씬을 직접 열고 실행하면 그 오브젝트가 존재하지 않아 로그가 통째로 사라진다.** 채택안은 이 경우에도 `GameBootstrapper` 가 초기화하므로 구멍이 없다. 또한 씬 파일·프리팹 배선이 늘어나 Inspector 누락 사고 지점이 하나 더 생긴다 |
| **D** | **`LoginBootstrapper` 가 `GameBootstrapper` 의 초기화 메서드를 호출** | 두 부트스트래퍼는 **서로 참조하지 않는다**는 현행 설계를 깬다(`LoginBootstrapper.cs` **14행** 주석: *"두 Bootstrapper 는 완전히 독립적이며, 서로 참조하지 않는다"*). 게다가 `GameBootstrapper` 인스턴스가 Login 씬에 없으므로 정적 메서드로 만들어야 하는데, 그러면 사실상 §6-3 후보 A 를 **가장 무거운 클래스 안에** 밀어 넣은 형태가 된다 |

---

## 6. 설계 판단 — 초기화 로직을 어디에 둘 것인가

현행 `InitializeLogging()` 은 `GameBootstrapper` 의 **private 메서드**(`GameBootstrapper.Setup.cs` 902행)라
`LoginBootstrapper` 가 호출할 수 없다. 공유 지점이 필요하다. 그 자리를 정하는 것이 이 작업의 유일한 설계 판단이다.

### 6-1. ⚠️ 최대 위험이자 설계를 결정하는 사실 — 세션 소유권

> **이 프로젝트는 정확히 이 영역에서 이미 사고를 겪었다.**
> MistShrine 힐 검증 작업에서 `GameBootstrapper.Awake`/`OnDestroy` 에 소유권 가드 없이 세션을 배선해
> **한쪽이 다른 쪽 세션을 닫았고, 로그 파일에 헤더만 남고 내용이 비었다.**
> 원인 추적에 왕복이 발생했고, 사용자가 *"지금까지 다른작업들은 문제없이 했는데 이번에만 못하네"* 라고 지적했다.
> 그 결과로 도입된 가드가 `FileSink._sessionOwned` 이며, `LogRules.md` **1.10** 의
> *"세션 소유권 문제도 구조적으로 없어진다"* 도 같은 사건에서 나왔다.

**현행 코드를 읽어 확인한 사실 — 기존 가드는 씬 사이를 건너가지 못한다.**

| # | 실측 사실 | 근거 |
|:-:|---|---|
| ① | `_logFileSink` 는 `GameBootstrapper` 의 **인스턴스 필드**다 | `GameBootstrapper.cs` **288행** |
| ② | `_sessionOwned` 는 `FileSink` 의 **인스턴스 필드**다 | `FileSink.cs` **92행** |
| ③ | 따라서 소유권 가드는 **"같은 인스턴스의 이중 열기·이중 닫기"만** 막는다. Login 이 만든 sink 인스턴스와 Game 이 만들 sink 인스턴스는 **서로의 플래그를 볼 수 없다** | ①②의 귀결 |
| ④ | `InitializeLogging()` 의 **첫 줄이 `GameLog.ClearSinks()`** 다 | `GameBootstrapper.Setup.cs` **907행** |
| ⑤ | `RuntimeLogger.BeginSession` 은 진입 즉시 **`EndSession()` 을 호출해 열린 스트림을 닫고 새로 연다** | `RuntimeLogger.cs` **69행** (주석: *"이미 열려 있는 스트림이 있으면 먼저 깔끔하게 닫고 새로 연다"*) |

**이 다섯이 합쳐지면 무슨 일이 벌어지는가 (Login 에서 그냥 열도록만 추가했을 때):**

```
Login 씬   : sink A 생성 → A.BeginSession()  → A._sessionOwned = true, 파일 스트림 열림
Game 씬    : GameBootstrapper.Awake
             → GameLog.ClearSinks()          ← ④ 등록돼 있던 A 가 목록에서 통째로 빠진다
             → sink B 생성 → B.BeginSession() ← B._sessionOwned 는 false 라 그대로 진행
             → RuntimeLogger.BeginSession     ← ⑤ A 가 열어 둔 스트림을 닫고 새로 연다
             ⇒ A 는 이 사실을 모른 채 _sessionOwned = true 로 남는다 (③)
그 뒤       : A 를 쥔 쪽이 EndSession() 을 호출하면 → B 의 스트림이 닫힌다
             ⇒ 2026-08-10 사고와 정확히 같은 구조
```

> **결론 (설계 결정) — 세션·sink 상태는 인스턴스가 아니라 `static` 소유자 한 곳에 있어야 한다.**
> "여는 쪽은 여럿, 닫는 쪽은 하나"는 **상태가 전역에 하나일 때만** 성립한다.
> `_sessionOwned` 가드는 **그대로 두되**(memory 기록: 건드리지 말 것), 그 위에 **정적 초기화 가드**를 하나 더 얹는다.

### 6-2. 함께 옮겨야 하는 것 — 전역 미처리 예외 훅 (사용자 §2 설계에 없던 항목)

`LogRules.md` **1.9** 가 요구하는 전역 예외 수집도 **같은 뿌리의 문제**를 갖는다.

| 실측 사실 | 근거 |
|---|---|
| 훅 등록/해제가 `GameBootstrapper` 의 `InitializeLogging`/`ShutdownLogging` 안에 있다 | `GameBootstrapper.Setup.cs` **949행** / **959행** |
| 콜백 `OnUnityLogMessageReceived` 가 **인스턴스 메서드**다 | `GameBootstrapper.Setup.cs` **997행** |
| 되먹임 방어 3겹 중 「방어 3」이 쓰는 `_lastHookCondition` / `_lastHookStackTrace` 가 **인스턴스 필드**다 | `GameBootstrapper.cs` **299행** / **305행** |

→ **로그인·로비 구간에서 터진 미처리 예외는 지금 아무 데도 수집되지 않는다.**
로그 커버리지만 고치고 훅을 두고 가면 **같은 구멍이 예외 쪽에 그대로 남는다.**
따라서 훅과 그 중복 방지 상태도 초기화 로직과 **함께** 정적 소유자로 옮긴다.
되먹임 방어 3겹(예외만 수집 / `GameLog.IsEmitting` / 직전 동일 예외 무시)은 **한 겹도 빼지 않고 그대로 이관**한다.

### 6-3. 후보 비교와 최종 선택

| 후보 | 장점 | 단점 / 기각 사유 |
|---|---|---|
| **A. `Infrastructure/Debug/` 에 새 정적 클래스** (가칭 `LogSessionBootstrap`) | · sink 인스턴스 · 초기화 플래그 · 훅 중복 방지 상태를 **한 곳의 `static` 에 모아 §6-1 을 구조적으로 해소**<br>· `FileSink`·`ConsoleSink`·`RuntimeLogger` 와 **같은 폴더·같은 레이어**라 참조가 자연스럽다<br>· `GameLog`(Application) 참조는 **Infrastructure → Application** 으로 **정상 방향**<br>· `UnityEngine.Application.logMessageReceived`/`quitting` 훅을 걸어야 하는데, 이 레이어는 이미 `UnityEngine` 을 참조한다(`FileSink.cs` 131행이 `UnityEngine.Application.dataPath` 사용) | 「조합 루트는 `GameBootstrapper` 하나」 원칙과의 관계를 정리해야 한다 → **아래 완화 근거** |
| **B. `GameLog` 확장** (Application 레이어) | 호출부가 가장 단순해진다 | ❌ **`Application → Infrastructure` 역참조 금지 위반.** 초기화 로직은 `FileSink`·`ConsoleSink` 를 생성해야 하는데 **둘 다 Infrastructure** 다. `LogRules.md` **1.13** 「`GameLog` 배치 위치」가 *"facade 와 인터페이스는 Application, 구현체만 Infrastructure — 의존 방향을 Infrastructure → Application 한 방향으로 유지"* 라고 이 방향을 명시적으로 막고 있다 |
| **C. 부트스트래퍼 공통 베이스 클래스** | 두 부트스트래퍼가 같은 진입점을 상속으로 공유 | ❌ **§6-1 이 해결되지 않는다** — 상태가 여전히 **인스턴스**에 남는다. 베이스에 `static` 필드를 두면 그건 결국 후보 A 를 어색한 위치(MonoBehaviour 베이스)에 놓은 것이다. 게다가 `GameBootstrapper` 는 partial 4파일(합계 약 129KB)의 대형 클래스라 상속 도입의 영향 범위가 이번 작업 범위를 넘어선다 |
| **D. `LoginBootstrapper` → `GameBootstrapper` 직접 호출** | — | ❌ §5-D 참조 |

#### ✅ 최종 선택: **후보 A**

**조합 루트 원칙과 충돌하지 않는 근거 (`LogRules.md` 1.8 · 프로젝트 아키텍처 제약):**

1. **이 클래스는 의존성을 주입하지 않는다.** 다른 객체에 무엇을 넘겨주지 않고,
   자기가 만든 sink 를 **자기 안에 보관**할 뿐이다. 조합 루트가 하는 일(그래프를 짜서 주입)을 하지 않는다.
2. **호출 주체는 여전히 부트스트래퍼(Bootstrap 레이어)뿐이다.** 즉 "누가 로그를 켰는가"는
   코드에서 계속 `LoginBootstrapper` / `GameBootstrapper` 로 추적된다.
   이 점이 기각안 B(`[RuntimeInitializeOnLoadMethod]`)와 결정적으로 다르다.
3. **선례가 이미 있다.** `Application/NetworkContext.cs` 가 이 프로젝트의 정적 홀더 선례이고,
   `LogRules.md` **1.13** 「`GameLog` 배치 위치」가 **바로 그 선례를 근거로 `GameLog` 자체를 정적 facade 로 확정**했다.
   정적 홀더를 하나 더 두는 것은 새 패턴을 만드는 것이 아니라 **기존 패턴을 따르는 것**이다.

> **구현 시 `game-programmer` 판단으로 남기는 항목:**
> 클래스 이름 · 메서드 시그니처(`EnsureInitialized()` / `Shutdown()` 등) · `quitting` 배선을 어디서 거는지
> (클래스 내부에서 자체적으로 걸지, 호출자가 거는지) · 파일 분리 여부.
> **이 문서가 확정하는 것은 「상태를 `static` 한 곳에 모은다」와 「Infrastructure/Debug 에 둔다」 두 가지뿐이다.**

---

## 7. 파일별 변경 계획

> 아래는 **현행 코드를 읽고 확정한 예상 변경 목록**이다. 행 번호는 변경 전 기준이며 구현 중 이동한다.

### [신규] `Assets/_Project/Scripts/Infrastructure/Debug/LogSessionBootstrap.cs` (파일명 가칭)

| 옮겨 오는 것 | 출처 | 비고 |
|---|---|---|
| `InitializeLogging()` 본문 | `GameBootstrapper.Setup.cs` **902~950행** | **로직을 바꾸지 않고 위치만 옮긴다.** 그 위에 정적 멱등 가드를 얹는다 |
| `ShutdownLogging()` 본문 | `GameBootstrapper.Setup.cs` **956~977행** | 호출 시점만 바뀐다(§4) |
| `OnUnityLogMessageReceived()` + 되먹임 방어 3겹 | `GameBootstrapper.Setup.cs` **997행~** | 인스턴스 메서드 → 정적 메서드 (§6-2) |
| `_logFileSink` / `_logConsoleSink` 필드 + `#if` 분기 | `GameBootstrapper.cs` **286~292행** | 인스턴스 → `static` (§6-1) |
| `_lastHookCondition` / `_lastHookStackTrace` 필드 | `GameBootstrapper.cs` **299행 / 305행** | 인스턴스 → `static` (§6-2) |

**새로 추가되는 것**
- **정적 멱등 가드** — 이미 초기화되어 있으면 즉시 반환한다. **`GameLog.ClearSinks()`(현행 907행)는 최초 1회에서만 실행되어야 한다.**
  두 번째 호출에서 이것이 돌면 앞 씬이 등록한 sink 가 빠져 §6-1 의 사고가 그대로 재현된다.
- **`UnityEngine.Application.quitting` 배선** — 종료 시 1회 정리. 반드시 **완전 수식**(§8 R4).
- 에디터 플레이모드 종료 대응 — **§8 R3 의 확인 결과에 따라** 보조 배선이 필요할 수 있다.

### [수정] `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs`

- `InitializeLogging()` → 본문을 신규 클래스로 이관하고, **위임 호출 한 줄로 축소**하거나 메서드 자체를 없앤다.
- `ShutdownLogging()` → 본문 이관. **호출부가 사라지므로 이 메서드는 주석 처리(비활성화)** — 최상단 고지 참조.
- `OnUnityLogMessageReceived()` → 이관.
- 894~896행의 섹션 주석(`로그 시스템 초기화 …`)은 이관 내용에 맞춰 정리한다.

### [수정] `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

| 행 | 현행 | 변경 |
|:-:|---|---|
| **468** | `InitializeLogging();` (Awake 첫 줄) | 신규 정적 클래스의 `EnsureInitialized()` 호출로 교체. **Awake 의 가장 앞이라는 위치는 그대로 유지** — 461~463행 주석이 그 이유를 이미 적어 두었다(*"로그는 그 뒤에 일어나는 모든 초기화를 관측할 수 있어야 한다"*) |
| **486** | `ShutdownLogging();` (OnDestroy) | **호출 제거(주석 처리).** 최상단 고지 참조 |
| **267~305** | sink 필드 + 훅 상태 필드 + 설명 주석 블록 | 신규 클래스로 이관. 남는 주석은 "로그 배선은 `LogSessionBootstrap` 이 담당한다"로 축약 |
| **473~480** | `OnDestroy` XML 주석 (*"로그 정리(ShutdownLogging)는 맨 뒤에 둔다"*) | 사실과 달라지므로 **함께 정정** — 정리 시점이 앱 종료로 옮겨졌다는 내용으로 |

### [수정] `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs`

| 위치 | 변경 |
|---|---|
| `Awake()` (**104~115행**) | **가장 앞줄**에 `EnsureInitialized()` 호출을 추가한다. 현행 첫 동작은 `PlayGamesPlatform.Activate()`(110행)인데, **그 활성화 자체와 그 뒤의 모든 초기화가 로그에 남아야** 하므로 로그가 먼저다. 근거는 `GameBootstrapper.cs` 461~463행이 이미 적어 둔 것과 동일하다 |
| **13행 주석** | *"GameBootstrapper 는 **Lobby**/Game 씬에 존재"* → 실측(§2-2)과 다르므로 **Game 씬에만 존재**로 정정. 이 주석이 틀린 채로 남으면 다음 사람이 "Lobby 는 커버된다"고 오판한다 |

### [변경 없음] `Assets/_Project/Scenes/Lobby.unity`

**씬 파일을 건드리지 않는다.** 근거:

1. **부트스트래퍼가 없고, 필요도 없다.** Login → Lobby 로 들어오면 세션이 **이미 열려 있어** 새로 열 것이 없다.
2. **씬 파일 수정은 비용이 크다.** 오브젝트/컴포넌트 추가는 병합 충돌과 Inspector 배선 누락(= 사고 지점 추가)을 부른다.
3. **다만 한계가 있다** — 에디터에서 `Lobby.unity` 를 **직접 열고 실행**하면 부트스트래퍼가 없어 sink 가 등록되지 않고
   `LogRules.md` **1.8** 의 콘솔 폴백만 탄다. §8 R6 의 판단 항목으로 남긴다.

### [변경 없음] `FileSink.cs` · `RuntimeLogger.cs`

**두 파일은 손대지 않는다.** `_sessionOwned` 소유권 가드(`FileSink.cs` 92행)와
`append: true` + `AutoFlush = true`(`RuntimeLogger.cs` 91행)는 과거 사고에서 나온 방어이므로 **그대로 유지**한다.
이번 변경은 **그 위에 정적 가드를 한 겹 더 얹는 것**이지, 기존 가드를 대체하는 것이 아니다.

---

## 8. 위험 요소

| ID | 위험 | 내용과 대응 |
|:--:|---|---|
| **R1** | **세션 소유권 (최대 위험)** | **§6-1 전체.** 이 프로젝트가 이미 한 번 사고를 낸 자리다 — 헤더만 남고 내용이 빈 파일. 멱등 가드를 **정적**으로 두지 않으면 그대로 재현된다. `FileSink._sessionOwned` 는 인스턴스 가드라 씬을 건너가지 못한다. **구현 시 가장 먼저 확인할 항목** |
| **R2** | **에디터 도메인 리로드 시 `static` 초기화** | `static` 필드는 도메인 리로드로 초기화된다. 초기화 플래그가 리셋되면 다음 진입에서 정상적으로 다시 열리므로 **정상 경로**지만, 리로드가 **비활성화**된 프로젝트에서는 플래그가 살아남아 "이미 초기화됨"으로 잘못 판정될 수 있다.<br>**실측:** `ProjectSettings/EditorSettings.asset` **27~28행** — `m_EnterPlayModeOptionsEnabled: 1` / `m_EnterPlayModeOptions: 0`. → **§9-①로 확인 필요** |
| **R3** | **`UnityEngine.Application.quitting` 이 에디터 플레이모드 종료에서도 발생하는가** | **확인하지 못했다 — 추정하지 않는다(§9-②).** 발생하지 않으면 파일 스트림이 닫히지 않은 채 남는다.<br>**완화 요인(실측):** `RuntimeLogger.cs` **91행**이 `AutoFlush = true` 이므로 **줄 단위로 이미 디스크에 반영**된다 → 스트림이 안 닫혀도 **기록된 로그가 유실되지는 않는다.**<br>**잔여 위험:** 스트림 누수와 다음 진입 시 중복 열기. → 확인 결과 발생하지 않으면 `#if UNITY_EDITOR` 에서 `EditorApplication.playModeStateChanged` 보조 배선을 검토 |
| **R4** | **네임스페이스 함정 — `Application`** | `Hexiege.Application` 이 존재해 수식 없는 `Application` 은 `UnityEngine.Application` 이 **아니다.** 이 프로젝트에서 실제로 **CS0234 3건**이 발생한 이력이 있다(커밋 `9fcee6b7` 로 해소). 신규 클래스는 `Hexiege.Infrastructure` 네임스페이스에 놓이므로 **`UnityEngine.Application.quitting` · `.logMessageReceived` 를 반드시 완전 수식**한다 |
| **R5** | **`LogLevel` 이름 충돌** | `LogLevel` 이 `Hexiege.Application` · `Hexiege.Infrastructure` 양쪽에 있다. 신규 클래스가 레벨 타입을 다룬다면 `FileSink.cs` 23~26행의 주석과 동일한 주의가 필요하다 |
| **R6** | **Lobby 씬 직접 진입은 여전히 미커버** | 채택안은 "먼저 뜨는 **부트스트래퍼**가 연다"인데 Lobby 에는 부트스트래퍼가 없다. 즉 §4 표의 *"Login 에서 연 세션이 그대로 살아 있음"* 은 **Login → Lobby 순서로 들어왔을 때만 참**이다. `Lobby.unity` 를 직접 열고 실행하면 콘솔 폴백만 탄다. → **§9-③ 사용자 판단 항목** |
| **R7** | **sink 이중 등록 → 콘솔 이중 출력** | `RuntimeLogger.Log` 는 파일과 콘솔에 **동시 출력**하므로, `FileSink` 와 `ConsoleSink` 가 함께 등록되면 콘솔에 같은 줄이 두 번 찍힌다(`GameBootstrapper.cs` 274~278행). 현행 `#if UNITY_EDITOR` 분기(에디터=FileSink / 빌드=ConsoleSink)를 **그대로 유지**하고, 멱등 가드가 두 번째 호출에서 확실히 반환하는지 확인한다. 검증 항목 **V7** |
| **R8** | **빌드 경로의 동작 변화** | 빌드에서는 파일 세션이 없고 `ConsoleSink` 만 등록된다. 이번 변경으로 **Login 씬부터 `ConsoleSink` 가 등록**되므로, 실기기 Logcat 에 로그인 구간 로그가 새로 나타난다. **이것은 의도된 개선**이며(§1 목적), `LogRules.md` **1.12** 의 캡처 도구로 파일에 뽑을 수 있게 된다 |

---

## 9. "확인 필요" 로 남기는 항목 (CLAUDE.md 규칙 10 — 추정하지 않는다)

| # | 항목 | 왜 확정하지 못했는가 | 누가 어떻게 확인하나 |
|:-:|---|---|---|
| **①** | **에디터 Enter Play Mode 설정의 실제 의미** | 실측값은 `m_EnterPlayModeOptionsEnabled: 1` / `m_EnterPlayModeOptions: 0` (`ProjectSettings/EditorSettings.asset` 27~28행). 이 조합에서 **도메인 리로드가 실제로 일어나는지**는 유니티 에디터에서 확인해야 하며, 파일 값만으로 단정하지 않는다 | `game-programmer` 가 에디터 Project Settings > Editor > Enter Play Mode Settings 에서 확인 |
| **②** | **`UnityEngine.Application.quitting` 이 에디터 플레이모드 종료 시 발생하는가** | 이 프로젝트 코드에 `quitting` 사용처가 **0건**이라 참고할 선례가 없다. 문서 담당은 코드를 실행할 수 없다 | `game-programmer` 가 구현 중 실제로 확인. **발생하지 않으면 §8 R3 의 보조 배선 도입** |
| **③** | **`Lobby.unity` 직접 진입을 커버할 것인가** (§8 R6) | 개발 편의상 Lobby 를 직접 열고 실행하는 빈도를 문서 담당이 알 수 없다. **추정하지 않는다** | **사용자 판단.** 선택지: ⓐ 그대로 둔다(콘솔 폴백) / ⓑ Lobby 에 최소 진입점을 둔다(씬 수정 발생) |
| **④** | **신규 클래스의 최종 이름·시그니처·`quitting` 배선 위치** | §6-3 에서 "상태를 static 한 곳에" 와 "Infrastructure/Debug 에 둔다" 까지만 확정했다 | **구현 시 `game-programmer` 판단** |
| **⑤** | **`ShutdownLogging()` 주석 처리분의 최종 삭제 시점** | WORKFLOW.md [4] 규정상 사용자 테스트 통과 전에는 삭제하지 않는다 | **[6] 통과 후 · [7] 전**에 삭제 |

---

## 10. 검증 방법 — 사용자가 실기로 확인할 항목

> 자연어로만 적는다. 판정 기준은 **"파일을 열었을 때 무엇이 보이는가"** 하나다.
> 확인 대상 파일: `Assets/_Project/Docs/_Logs/_editor/{오늘 날짜}/RuntimeLog.txt`

| ID | 확인 내용 | 성공 판정 |
|:--:|---|---|
| **V1** | 게임을 **로그인 화면부터** 시작해서 로그인하고, 로비를 거쳐 랜덤매칭으로 한 판을 끝낸다. 그런 다음 오늘 날짜 폴더의 로그 파일을 연다 | **머리말 바로 다음 줄부터 "로그인 화면에서 일어난 일"이 적혀 있다.** 지금처럼 곧바로 전투 관련 줄로 시작하면 **실패** |
| **V2** | 같은 파일을 위에서 아래로 읽어 내려간다 | **로그인 → 로비 → 전투가 끊기지 않고 한 번에 이어진다.** 중간에 머리말(`=== ... ===`)이 다시 나타나면 세션이 끊긴 것이므로 **실패** |
| **V3** | 게임을 종료하고 **전투 화면을 직접 열어서** 실행한다 | 지금까지처럼 **로그 파일이 정상적으로 쌓인다.** 이 경우가 깨지면 기존 기능이 퇴행한 것이므로 **실패** |
| **V4** | 일부러 **잘못된 비밀번호로 로그인**을 시도한다 | 그 실패가 **파일에 남는다.** 어떤 이유로 실패했는지가 함께 적혀 있어야 한다 |
| **V5** | 파일 전체를 훑어 **개인정보가 그대로 보이는 곳이 없는지** 확인한다 | **이메일 주소가 한 건도 없고**, 계정 식별자가 원본 그대로 보이지 않는다. (§2-3 — 마스킹 15곳 중 14곳을 파일로 검증할 수 있게 되는 것이 이번 작업의 부수 성과다) |
| **V6** | 플레이를 멈췄다가 **다시 실행**하고 파일을 확인한다 | **같은 파일에 머리말이 새로 한 번 찍히고, 그 아래로 계속 쌓인다.** 파일이 새로 생기거나 앞 내용이 사라지면 **실패** |
| **V7** | 유니티 콘솔 창을 본다 | **같은 줄이 두 번 찍히지 않는다.** 두 번 찍히면 기록 담당이 중복 등록된 것이므로 **실패** (§8 R7) |
| **V8** | 로그인·로비 구간에서 오류가 났을 때(재현 가능한 경우) 파일을 본다 | 예상하지 못한 오류도 파일에 남는다 (§6-2 — 전역 예외 수집이 로그인 구간까지 확장됐는지) |

> **에디터 실기만으로 충분하다.** 파일 기록은 에디터 전용이고(`FileSink` 전체가 `#if UNITY_EDITOR`),
> 빌드 쪽 확인은 필요 시 `LogRules.md` **1.12** 의 Logcat 캡처 도구로 별도 수행한다.

---

## 11. 이번 작업의 범위 밖 (명시)

아래는 **이번 작업에서 하지 않는다.** 필요하면 각각 별도 task 로 분리한다.

| 항목 | 근거 |
|---|---|
| **나머지 계층 `Debug.Log` 234건의 `GameLog` 이관** | `LogRules.md` **1.13** — 네트워크·인증 8파일 205건 밖의 잔존분. **사용자가 이미 별도 task 로 분리하기로 결정**했다 |
| **`NetworkCombatController.cs` 잔존 raw `Debug.Log` 11건** | 위 234건에 포함된다. 128행의 중복 로그도 그때 처리한다 |
| **`FileSink.EditorLogsRootRelativeToAssets` 가 `private` 이라 `LogcatCapture.cs` 가 경로 문자열을 복제하는 문제** | `LogRules.md` **1.13** 이 *"이번 범위 밖이라 하지 않았다"* 로 남겨 둔 항목. 이번에도 범위 밖 |
| **운영 로그의 서버 전송 구현** | `LogRules.md` **1.1** 이 *"서버 수집은 향후 작업"* 으로 규정 |
| **로그 분류(축 A/축 B) 재판정, 이벤트 키 추가** | 이번 작업은 **배선 위치만** 바꾼다. 로그 내용·분류는 건드리지 않는다 |
| **`LogRules.md` 본문 개정** | 구현·검증이 끝난 뒤 WORKFLOW.md **[11]** 문서 반영 단계에서 수행한다. 특히 **1.13 「현행 코드와의 차이」** 와 **1.8 「등록 주체」** 서술이 갱신 대상이 된다 |
| **`Lobby.unity` 씬 수정** | §7 [변경 없음] 참조. §9-③ 에서 사용자가 ⓑ를 택하면 그때 범위에 넣는다 |

---

## 12. 요약

| 항목 | 내용 |
|---|---|
| **목적** | 로그인·로비 구간의 로그 **73건**이 파일에 닿게 한다 (어느 화면에서 시작하든 기록) |
| **핵심 변경** | 여는 쪽 = 먼저 뜨는 부트스트래퍼(멱등) / 닫는 쪽 = 앱 종료 1회 |
| **설계 판단** | 세션·sink·훅 상태를 **`Infrastructure/Debug` 의 정적 클래스 한 곳**에 모은다 (§6-3 후보 A) |
| **최대 위험** | 세션 소유권 — 인스턴스 가드는 씬을 건너가지 못한다 (§6-1) |
| **확인 필요** | 5건 (§9) |
| **다음 단계** | **사용자 승인 → `game-programmer` 위임** |
