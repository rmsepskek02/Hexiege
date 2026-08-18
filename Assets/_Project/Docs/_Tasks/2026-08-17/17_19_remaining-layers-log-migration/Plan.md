# Plan — 나머지 계층 로그 이관 (raw `Debug.Log` 204건 → `GameLog`)

> **이 문서는 Plan.md 한 개만 작성한다.** 사용자 지시에 따라 `Research.md` 는 만들지 않는다.
> 조사 내용은 이 문서의 **§4 실측 데이터** 와 **§5 결정 사항** 안에 근거와 함께 포함했다.

---

## 서두 — 무엇을 왜 하는가 (일반 언어 설명 · CLAUDE.md 규칙 13)

**지금 이 게임의 로그는 딱 절반만 새 방식입니다.**

우리는 얼마 전에 로그를 크게 뜯어고쳤습니다. 그전까지 로그는 유니티 콘솔 창에만 찍히고
사라지는 물건이었습니다. 콘솔 로그는 그 순간 개발자가 화면을 보고 있어야만 쓸모가 있고,
꺼지면 아무것도 남지 않습니다. 그래서 **파일로 남기고, 나중에는 서버로 보내서
"이 문제가 몇 명에게 몇 번 일어났는지" 를 숫자로 볼 수 있게** 만드는 방식으로 바꿨습니다.
그렇게 만든 것이 `GameLog` 입니다.

문제는 **그 전환이 게임 전체가 아니라 8개 파일에서만 끝났다**는 점입니다.
지금 게임 코드에는 새 방식과 옛 방식이 나란히 섞여 있습니다.

그래서 실제로 이런 일이 벌어집니다. 플레이어가 게임에 접속하면
**로그인은 파일에 남고 → 매칭도 파일에 남는데 → 그 바로 다음 단계인 "상대와 연결하기"(Relay)가
실패하면 그건 파일에 안 남습니다.** 콘솔에만 찍히고 사라집니다.
나중에 "왜 접속이 안 됐냐" 를 추적하려고 로그 파일을 열면, 딱 그 자리만 비어 있습니다.

**이 작업은 남아 있는 204곳을 전부 새 방식으로 옮겨서, 그 구멍을 메우는 일입니다.**
코드가 하는 일 자체는 하나도 바뀌지 않습니다. "어디에 기록하느냐" 만 바뀝니다.

---

## 1. 왜 하는가

### 1-1. 사용자가 이 개편을 시작한 이유 (원문)

> "모든 로그를 우리가 만들었던 로그방식에 맞춰서 수정하는게 좋을거같은데?
> **콘솔로그는 활용가치가 낮다고 생각해.** 클로드를 통해서 분석가능하도록 로그를 기록하고,
> **서버에도 전송하여 지표로 수집할수있는 로그가 활용가치가 높기 때문에** 많은 개편을 한거였어."

이 문장이 이 작업의 판정 기준이다. 아래 §5 의 모든 결정은 여기로 되돌아가서 판단한다.

### 1-2. 지금 코드가 자기 규칙을 어기고 있다

`LogRules.md` **1.14 금지 사항 1** 은 이렇게 규정한다.

> **진단용 raw `Debug.Log` 직접 호출 금지** — 반드시 `GameLog`(구현 전까지는 `RuntimeLogger`)를 경유한다.
> 콘솔에만 남은 로그는 파일이 되지 않아 **Claude가 읽을 수 없다.**

**그런데 204곳이 raw `Debug.Log` 로 남아 있다.** 규칙과 코드가 어긋난 상태다.

### 1-3. 왜 이렇게 됐는가 — 정직하게 기록한다

**"8파일" 이라는 경계에는 설계상의 근거가 없다.**

선행 task `_Tasks/2026-08-13/07_13_network-auth-log-cleanup/` 는 원래
*"네트워크·인증 계층에 쌓인 진단 로그 94건을 삭제한다"* 로 시작했다.
그런데 **무엇을 지우고 무엇을 남길지 판정할 기준이 없었다.**
그 기준을 만드는 과정에서 `LogRules` 의 두 축(심각도 · 존속)이 나왔고,
그 축을 코드로 옮기는 과정에서 `GameLog` / `LogEvent` / `ILogSink` 체계가 나왔다.

**즉 8파일은 "여기까지가 한 계층" 이라서 고른 경계가 아니라, 애초에 청소하려던 대상 목록이었을 뿐이다.**
그 목록이 끝나면서 이관도 함께 멈췄다.

그 결과 지금은 **같은 `Infrastructure/Network` 폴더 안에서도 두 방식이 공존한다.**
`LobbyManager` 는 `GameLog` 를 쓰고, 바로 옆 `RelayManager` 는 raw `Debug.Log` 를 쓴다.

### 1-4. 어중간한 상태의 실제 비용

**파일 로그가 반쪽이라 라이브 버그 추적 시 흐름 중간에 구멍이 생긴다.**

가장 뚜렷한 예가 접속 실패 경로다.

| 단계 | 담당 코드 | 현재 상태 |
|---|---|---|
| 로그인 | `FirebaseAuthService` / `LoginUseCase` | ✅ 이관 완료 — **파일에 남는다** |
| UGS 초기화 | `UnityServicesInitializer` | ✅ 이관 완료 — **파일에 남는다** |
| 매칭 로비 | `LobbyManager` / `NetworkGameManager` | ✅ 이관 완료 — **파일에 남는다** |
| **Relay 연결** | **`RelayManager` (13건)** | ❌ **미이관 — 콘솔에만 찍히고 파일에 안 남는다** |
| 세션 시작 | `NetworkGameFlow` (10건) | ❌ 미이관 |
| 인게임 동기화 | `NetworkHealthSync`(14) · `NetworkTileSync`(8) 등 | ❌ 미이관 |

**흐름의 앞쪽은 기록되는데 뒤쪽이 기록되지 않는다.**
"로그인은 됐는데 게임에 못 들어갔다" 는 제보를 받았을 때, 로그 파일은
*"매칭까지는 정상이었다"* 까지만 말해 주고 **그 다음에 무슨 일이 있었는지는 말해 주지 못한다.**
이건 로그가 아예 없는 것보다 나은 정도이지, 원인 추적에는 못 미친다.

또한 **씬 무관 로그 수집(커밋 `a253232e`)으로 파일 커버리지를 로그인 구간까지 넓혀 놓은 효과가
반감된다.** 파일은 열려 있는데 정작 그 구간의 코드가 파일에 안 쓰는 방식을 쓰고 있기 때문이다.

---

## 2. 근거 규칙

### 2-1. `GameSystemRules` — **해당 없음**

`Assets/_Project/Docs/GameSystemRules.md` 인덱스를 읽고 확인했다.
등재된 13개 파일은 맵 · 랜덤맵 · UI · 유닛 · 건물 · 스킬 · 강화 · Canvas SortingOrder · 사운드 · AI(+시나리오 3종)이며,
**로그 체계를 규정하는 항목이 없다.** 이 작업은 게임 시스템 동작을 바꾸지 않으므로
`GameSystemRules` 하위 문서에 근거를 둘 항목이 **한 건도 없다.**

→ **근거 규칙의 단일 소스는 `Assets/_Project/Docs/LogRules.md` 다.**

### 2-2. 이 작업이 근거로 삼는 `LogRules` 조항

| 조항 | 내용 | 이 작업에서의 역할 |
|---|---|---|
| **1.2** | 두 축 — 심각도(A) / 존속(B) | 204건 각각을 판정하는 기본 잣대 |
| **1.3** | 분류 원칙 1~4 + 원칙 간 우선순위 | 경계 사례 판정. 특히 **원칙 1 > 원칙 3** |
| **1.5** | 이벤트 키 `LogEvent` | **`운영` 로그에만** 키 부여. §5-2 의 근거 |
| **1.6** | 민감 데이터 (이메일 금지 / UID·PlayerId 해시) | §5-3 의 근거 |
| **1.7** | 릴리스 스트리핑 `[Conditional]` | `개발` = `GameLog.Dev`, `운영` = `GameLog.Ops` 로 나뉘는 근거 |
| **1.11** | `RuntimeLogger` 의 위치 — **`임시` 계층** | §5-1(에디터 도구 제외 여부)의 핵심 근거 |
| **1.14 금지 사항 1** | raw `Debug.Log` 직접 호출 금지 | **이 작업 전체의 근거** |
| **1.14 금지 사항 9** | 같은 사건 두 곳 로깅 금지 | `NetworkCombatController` 중복 처리 근거 |

---

## 3. 작업 범위 — 이관 대상 / 대상 아님

### 3-1. 이관 대상이 **아닌** 30건 — 로그 시스템 자체

**`GameLog` 로 바꾸면 자기 자신을 호출하는 무한 루프가 된다.**

| 파일 | 건수 | 제외 근거 |
|---|:-:|---|
| `Application/GameLog.cs` | 9 | sink 가 0개일 때의 **콘솔 폴백 구현**. `LogRules` **1.8** 「sink가 하나도 없으면 콘솔로 폴백한다」가 요구하는 동작 그 자체 |
| `Application/Interfaces/ILogSink.cs` | 2 | **주석**. 실행 코드가 아니다 |
| `Infrastructure/Debug/RuntimeLogger.cs` | 8 | 파일 쓰기 구현체 (**1.11**) |
| `Infrastructure/Debug/LogSessionOwner.cs` | 5 | sink 등록·세션·예외 훅 소유자 (**1.8** / **1.9**) |
| `Infrastructure/Debug/ConsoleSink.cs` | 5 | **콘솔 출력 구현체.** `Debug.Log` 를 부르는 것이 이 클래스의 존재 이유다 |
| `Infrastructure/Debug/FileSink.cs` | 1 | 파일 sink 구현체 |
| **합계** | **30** | |

### 3-2. 이관 대상 204건 / 50파일

---

## 4. 실측 데이터

**측정 기준:** `grep -rn "Debug\.Log" Assets/_Project/Scripts --include=*.cs`
**측정 일시:** 2026-08-17 (이 Plan 작성 시점에 직접 재실측)

전체 잔존 **234건** = 이관 대상 **204건** + 대상 아님 **30건**.
`grep -rn`(줄 수)과 `grep -rno`(출현 수)가 **둘 다 234** 로 일치했다 —
**한 줄에 두 개가 들어간 자리는 없다.**

### 4-1. 그룹별

| 그룹 | 건수 | 파일 수 |
|---|:-:|:-:|
| `Infrastructure/Network` | **95** | 14 |
| `Presentation` (UI·오디오·입력·그리드) | 39 | 20 |
| `Bootstrap` | 26 | 5 |
| `Editor` 도구 | 16 | 4 |
| `Application` | 9 | 2 |
| `Infrastructure/Cloud` | 8 | 2 |
| `Infrastructure/Factories` | 7 | 2 |
| `Debug/` (테스트용) | 4 | 1 |
| **합계** | **204** | **50** |

### 4-2. 파일별 상위 10 — ⚠️ 사전 집계 1건 정정

| 파일 | 건수 | 비고 |
|---|:-:|---|
| `Infrastructure/Network/NetworkHealthSync.cs` | 14 | |
| `Infrastructure/Network/RelayManager.cs` | 13 | §1-4 의 구멍 |
| **`Bootstrap/GameBootstrapper.Setup.cs`** | **13** | ⚠️ **사전 집계의 「18」은 실측과 다르다.** 아래 참조 |
| `Infrastructure/Network/NetworkCombatController.cs` | 11 | **1.13** 이 지목한 중복 1건 포함 |
| `Presentation/UI/LobbyUI.cs` | 10 | |
| `Infrastructure/Network/NetworkGameFlow.cs` | 10 | |
| `Infrastructure/Network/NetworkUnitMovementController.cs` | 9 | |
| `Infrastructure/Network/NetworkTileSync.cs` | 8 | |
| `Bootstrap/LoginBootstrapper.cs` | 8 | |
| `Application/UseCases/AccountLinkUseCase.cs` | 8 | |

> **⚠️ 정정 — `GameBootstrapper.Setup.cs` 는 18건이 아니라 13건이다.**
> 실측 줄 번호: **62 · 100 · 126 · 235 · 564 · 787 · 797 · 810 · 832 · 871 · 880 · 886 · 893** = **13건**.
> **`Bootstrap` 그룹 합계 26건은 정확하다** — 내역은
> `GameBootstrapper.Setup.cs` **13** + `LoginBootstrapper.cs` **8** + `GameBootstrapper.Network.cs` **3**
> + `GameBootstrapper.cs` **1** + `GameBootstrapper.Map.cs` **1** = **26**.
> 즉 **그룹 총계는 맞고 파일 단위 배분만 어긋나 있었다.** 총 204건 계산에는 영향이 없다.

나머지 40파일은 **1~7건**짜리다. 그중 **14파일이 1건짜리**다(전부 `Presentation`).

---

## 5. 결정 사항 ← 이 Plan 의 핵심

### 5-1. ⭐ `Editor` 도구 16건 + `Debug/` 4건 — 이관할 것인가

**판정 기준은 §1-1 의 사용자 기준이다: "서버 지표로 수집할 가치가 있는가."**

#### ① 검토 — `LogRules` 1.11 「`임시` 계층」에 해당하는가

`LogRules` **1.11** 은 `GameLog` 를 거치지 않아도 되는 예외를 **딱 하나** 인정한다.

> `임시` 로그는 `GameLog`를 거치지 않아도 된다. 이벤트 키도, 스트리핑 특성도 필요 없다.
> **작업이 끝나면 코드째로 사라질 것**이기 때문이다. 사라질 코드에 규격을 씌우는 것은 비용만 늘린다.

**에디터 도구 4파일은 이 예외에 해당하지 않는다.** 근거는 하나다 —
**이 파일들은 사라질 코드가 아니다.** 리포지토리에 커밋되어 상주하는 도구이고,
`AndroidBuildAssetOptimizer` 는 빌드 파이프라인 도구로 `AABSizeOptimization.md` ·
`BuildAssetOptimizationReport.md` 가 참조하는 상시 자산이다.
1.11 의 근거 문장("작업이 끝나면 코드째로 사라질 것")이 성립하지 않으므로 **1.11 을 근거로 제외할 수 없다.**

#### ② 검토 — 빌드에 포함되는가 (실측)

| 대상 | 빌드 포함 여부 | 근거 (실측) |
|---|---|---|
| `Assets/_Project/Scripts/Editor/` 4파일 · 16건 | **포함되지 않음 (확정)** | ① 폴더명이 `Editor` 다 — 유니티가 **특수 폴더로 취급해 플레이어 빌드에서 제외**한다. ② 4파일 전부 `UnityEditor` 네임스페이스를 참조한다(각 1건 확인) — 빌드에 포함되면 애초에 컴파일이 되지 않는다 |
| `Assets/_Project/Scripts/Debug/UIManagerTestButtonHandler.cs` · 4건 | ⚠️ **포함된다** | **`Debug/` 는 유니티 특수 폴더가 아니다.** 이 파일은 `public class UIManagerTestButtonHandler : MonoBehaviour` 이고, **`#if UNITY_EDITOR` 가드가 한 줄도 없다**(실측: `UNITY_EDITOR` · `DEVELOPMENT_BUILD` 출현 **0건**) |

> **⚠️ 사전 전제 정정.** 이 task 의 사전 정리는 에디터 도구와 `Debug/` 를 묶어
> *"빌드에 들어가지 않아 서버 전송 대상이 아니고"* 라고 적었으나,
> **`Debug/UIManagerTestButtonHandler.cs` 4건에 대해서는 사실이 아니다.** 이 파일은 릴리스 빌드에 그대로 들어간다.

#### ③ 결론

| 대상 | 결정 | 근거 |
|---|---|---|
| **`Editor/` 4파일 16건** | **이관하지 않는다 (제외)** | 아래 |
| **`Debug/` 1파일 4건** | **이관한다** | 아래 |

**`Editor/` 16건을 제외하는 근거:**

1. **서버 전송 대상이 될 수 없다.** 빌드에 포함되지 않는 코드는 플레이어 기기에서 실행되지 않으므로,
   §1-1 이 말하는 *"서버에 전송하여 지표로 수집"* 의 대상이 **원리적으로 될 수 없다.**
2. **`LogRules` 1.2 축 B 판정이 자동으로 `개발` 로 확정된다.** 축 B 1번 질문은
   *"플레이어 기기에서만 벌어지는가"* 인데, 에디터 전용 코드는 **항상 "아니오"** 다.
   따라서 `운영` 이 될 수 있는 건이 0건이고, `LogEvent` 키도 필요 없다(1.5).
3. **`[Conditional]` 스트리핑(1.7)이 무의미하다.** `GameLog.Dev` 의 존재 이유는
   릴리스 빌드에서 호출을 지우는 것인데, 애초에 릴리스에 들어가지 않는 코드다.
4. **파일 기록 이득도 없다.** `FileSink` 는 동작 전체가 `#if UNITY_EDITOR` 안에 있어
   에디터에서는 raw `Debug.Log` 도 어차피 콘솔에 뜬다. 얻는 것은 형식 통일뿐이고,
   그 대가로 `AndroidBuildAssetOptimizer`(빌드 파이프라인) 같은 검증된 도구를 건드리게 된다.

**`Debug/` 4건을 이관하는 근거:**

1. **빌드에 포함된다**(위 ② 실측). 즉 §1-1 의 기준에 그대로 걸린다.
2. **1.11 의 `임시` 예외에 해당하지 않는다** — 커밋되어 상주하는 파일이다.
3. 다만 내용은 UI 테스트용이므로 **축 B 판정은 `개발` 이 될 가능성이 높다**(`GameLog.Dev`).
   그러면 `[Conditional]` 로 **릴리스에서 코드가 사라지는 이득**까지 생긴다 —
   지금은 가드가 없어 **릴리스 빌드에서 문자열 보간까지 그대로 실행된다.**

#### ④ 후속 항목 — `LogRules` 에 예외를 명문화할 것인가

**결론: 명문화한다. 단 이 task 의 범위가 아니라 후속 항목으로 분리한다.**

- **명문화가 필요한 이유:** **1.14 금지 사항 1** 은 예외를 두지 않는데 우리는 16건을 제외하기로 했다.
  근거를 Plan 에만 남기면 **다음에 같은 질문이 또 나오고, 그때는 이 판단이 기억나지 않는다.**
  `LogRules` 가 규칙의 단일 소스이므로 예외도 거기에 있어야 한다.
- **이 task 에서 하지 않는 이유:** `LogRules` 개정은 **규칙 문서 변경**이고, 이 task 는 **코드 이관**이다.
  CLAUDE.md 규칙 6(작업 범위 초과 금지)에 따라 묶지 않는다.
- **제안 문구(후속 task 용 초안):**
  > 1.14 금지 사항 1 단서 — **에디터 전용 코드(`Editor/` 특수 폴더 하위)는 이 금지의 대상이 아니다.**
  > 빌드에 포함되지 않아 축 B 가 항상 `개발` 로 확정되고, 서버 수집 대상이 될 수 없기 때문이다.
  > 단 **`Debug/` 처럼 이름만 개발용이고 실제로는 빌드에 포함되는 폴더는 이 단서에 해당하지 않는다.**

> **→ 이 항목은 사용자 승인이 필요하다.** Plan 승인 시 함께 판단을 요청한다.

---

### 5-2. `LogEvent` 키 — 재사용 vs 신설 기준

현재 `LogEvent` 는 **멤버 32개**다(`Unknown`(0) 포함 — 실측 재확인).
32개 전부가 **네트워크·인증 계층**에서 나온 키다.

#### 확정 기준

**[기준 0] 먼저 축 B 를 판정한다. `개발` 이면 키 자체가 필요 없다.**

`LogRules` **1.5** 적용 범위: *"**`운영` 로그만** 키를 받는다. `개발`·`임시`는 전송 대상이 아니므로 불필요하다."*

이것이 **가장 중요한 필터**다. 선행 task 실적은 205건 중 `개발` **120** / `운영` **85** 로
**58%가 `개발`** 이었다. 이번 204건도 비슷한 비율이면 **키를 고민할 대상은 100건 안쪽**이다.
→ **키 논의를 하기 전에 축 판정을 먼저 끝낸다.** 순서를 뒤집으면 필요 없는 키를 만들게 된다.

**[기준 1] 기본은 재사용이다.**

근거는 **1.5 「이름 짓기」** 다.

> **무엇이 일어났는지**를 적는다. 어디서 일어났는지는 `[System/Class]`가 이미 담고 있다

→ **발생 위치가 다르다는 것은 새 키를 만들 이유가 되지 못한다.** 위치는 이미 다른 필드가 담고 있다.
예: `Infrastructure/Factories/UnitFactory.cs` 에서 조합 루트를 못 찾는 상황은
기존 `NetworkControllerSpawnedWithoutGameServices` 계열과 **같은 사건**이면 그 키를 그대로 쓴다.

**[기준 2] 신설 조건 — "집계했을 때 다른 대응을 요구하는가"**

아래 **둘 다** "예" 일 때만 신설한다.

1. **이 사건의 발생 건수가 기존 어떤 키에도 흡수되면 안 되는가?**
   (흡수되면 두 문제가 한 지표에 섞여, 어느 쪽이 늘었는지 알 수 없게 된다)
2. **이 사건이 늘었을 때 취할 조치가 기존 키의 조치와 다른가?**
   (조치가 같다면 굳이 지표를 나눌 이유가 없다)

**[기준 3] 판단이 갈리면 신설하지 않고 사용자에게 확인한다.**

`LogRules` **1.5** 는 *"이름은 **한 번 정하면 바꾸지 않는다.** 이름이 바뀌면 서버에 쌓인 과거 지표와 연결이 끊긴다"* 고 규정한다.
**되돌리기 비용이 비대칭이다** — 키를 늦게 추가하는 것은 싸고, 잘못 만든 키를 지우는 것은 비싸다.
→ **애매하면 만들지 않는다.** (CLAUDE.md 규칙 12)

**[기준 4] `Unknown` 은 어떤 경우에도 쓰지 않는다.**

`LogRules` **1.13** 실측: *"`운영` 85건 전부에 키가 부여되었고 **`Unknown` 사용처는 0건**"*.
**이 0건을 깨지 않는다.** 키가 애매해서 `Unknown` 을 쓰느니 축 판정을 다시 한다.

#### 예상 — 신설 압력이 큰 그룹

| 그룹 | 기존 키 커버리지 | 비고 |
|---|---|---|
| `Infrastructure/Network` 95건 | **높음** — 32개 키가 전부 이 계층 출신 | 대부분 재사용 예상 |
| `Bootstrap` 26건 | 낮음 | 다만 **설정 오류가 많아 `개발` 판정 비중이 클 것**(1.3 원칙 3 단서 — Inspector 배선 누락은 `Warn`+`개발`) → 키 불필요 |
| `Presentation` 39건 | 낮음 | UI 통지가 이미 있는 자리가 많아 **원칙 2 로 `개발`** 예상 → 키 불필요 |
| `Infrastructure/Cloud` 8건 | 낮음 | **신설 가능성이 가장 높은 그룹** (§5-3 참조) |

---

### 5-3. 민감 데이터 (`LogRules` 1.6) — **이번엔 미루지 않고 이관과 동시에 처리한다**

#### ① 방침 확정

**마스킹을 별도 단계로 미루지 않는다. 이관하는 그 자리에서 함께 적용한다.**

**근거:** 선행 task 는 마스킹을 **5단계로 분리**했고, 그 결과
① 코드에 `⚠️ 5단계(마스킹) 대상` 표식 주석이 15곳 남은 채로 상당 기간 방치되었고,
② **감사표(`LogAudit.md` §6)가 누락한 1곳(`LobbyManager` 의 `HostId` 평문 출력)이
나중에야 발견**되었다(`LogRules` 1.13 기록 — *"이 자리는 `LogAudit.md` §6 목록에 없었다"*).
**분리하면 목록이 진실의 원본이 되고, 목록이 틀리면 누락이 남는다.**
이관하면서 그 자리의 실제 코드를 보고 처리하면 목록에 의존하지 않는다.

#### ② 실측 — 이번 대상의 민감 데이터 현황

계정 계층 3파일(총 16건)을 직접 열어 확인했다.

| 파일 | 건수 | 실측 결과 |
|---|:-:|---|
| `Application/UseCases/AccountLinkUseCase.cs` | 8 | **UID·이메일·토큰 원본 출력 0건.** 8건 모두 고정 문구 또는 `e.Message` 만 출력한다(78·85·93·96·117·124·133·136행). *"이메일 연동 성공"* 처럼 **단어로만** 등장하고 값은 없다 → **마스킹 대상 아님** |
| `Infrastructure/Cloud/LeaderboardService.cs` | 3 | **3건 모두 `e.Message` 만**(96·127·182행) → **마스킹 대상 아님** |
| `Infrastructure/Cloud/PlayerProfileService.cs` | 5 | 3건은 `e.Message`(94·122·151행). **2건이 `{nickname}#{code}` 를 출력한다**(118·147행) → **아래 ③ 판단 필요** |

> **사전 정리는 이 세 파일을 "계정 계층이라 가능성이 높다" 고 봤으나, 실측 결과 UID·PlayerId·이메일·토큰의 평문 출력은 0건이었다.**
> 남은 쟁점은 **닉네임** 하나다.

#### ③ ⚠️ 판단 필요 — 닉네임은 민감 데이터인가

`LogRules` **1.6** 의 표는 **이메일 / UID·PlayerId / 토큰·세션 키** 세 항목만 규정하고,
**닉네임은 어느 항목에도 없다.**

**추정하지 않는다**(CLAUDE.md 규칙 10). 양쪽 논거를 적고 사용자 판단을 받는다.

| 마스킹해야 한다는 논거 | 마스킹하지 않아도 된다는 논거 |
|---|---|
| 닉네임은 **플레이어가 직접 입력한 문자열**이라 본명·연락처가 들어갈 수 있다 | 닉네임은 **게임 안에서 다른 플레이어에게 공개**되는 값이다. 애초에 비공개 정보가 아니다 |
| 1.6 서두: *"로그 파일은 개발자 간 공유되고, 채팅에 붙여지고, **리포지토리에 커밋된다**"* — `_Logs/_editor/` 는 실제로 커밋된다(1.10) | `#code` 와 함께면 **계정 식별자로 기능**해 "누가 겪었나" 추적에 실제로 쓸모가 있다 |
| 해시로 치환해도 "같은 사람인지"는 알 수 있다(1.6 UID 논거와 동일) | 해시로 바꾸면 **로그를 사람이 읽을 때 누구인지 모르게 된다** — 닉네임 저장 실패를 추적하는 로그인데 정작 대상을 알 수 없다 |

**→ 사용자 확인 사항 A: `PlayerProfileService.cs` 118·147행의 닉네임 출력을 (a) 그대로 둘 것인가,
(b) `GameLog.HashId` 로 해시할 것인가, (c) 아예 값을 빼고 성공/실패만 남길 것인가.**

승인 전까지 이 2건은 **이관하되 값 표기는 현행 유지**하고, 결정 후 별도로 반영한다.

#### ④ 🔴 이관이 아니라 **신규 추가**가 필요한 자리를 발견했다

**`PlayerProfileService.cs` 의 삼킨 예외 5곳에 로그가 한 줄도 없다.**

실측 — `GetString` / `GetInt` / `GetBool` 의 `catch` 블록:

| 줄 | 코드 | 로그 |
|:-:|---|---|
| 174 | `catch { return fallback; }` (`GetString`) | **없음** |
| 185 · 189 | `catch { ... catch { return fallback; } }` (`GetInt`) | **없음** |
| 201 · 204 | `catch { ... catch { return fallback; } }` (`GetBool`) | **없음** |

**그런데 `LogRules` 1.2 조합표는 바로 이 자리를 이미 판정해 두었다.**

> | CloudSave 값 변환 실패 → 기본값 사용 | `Infrastructure/Cloud/PlayerProfileService.cs` `GetString`/`GetInt`/`GetBool` — `catch { return fallback; }` | `Warn` | **운영** | **삼킨 예외.** 실패해도 프로필이 "멀쩡한 기본값"으로 보여 아무도 눈치채지 못한다 |

그리고 **1.3 원칙 4** 는 *"삼킨 예외(`catch { }`)는 **반드시** `운영` 로그를 남긴다"* 고 규정한다.

**즉 규칙이 `운영` 로그를 명시적으로 요구하는 자리에 로그가 아예 없다.**
이것은 "raw `Debug.Log` 를 `GameLog` 로 바꾸는" 이관이 아니라 **로그 신규 추가**다.

**→ 사용자 확인 사항 B: 이 5곳(`운영` 로그 신규 추가 + 신규 `LogEvent` 키 1개
— 1.5 예시가 이미 `CloudSaveValueParseFailed` 를 들고 있다)을 이번 범위에 포함할 것인가.**

- **포함 찬성:** 1.2 조합표가 지목한 자리이고, 같은 파일을 어차피 건드린다. 지금 안 하면 또 미뤄진다.
- **포함 반대:** 이 task 는 *"기존 로그를 옮긴다"* 이고, 신규 추가는 **범위 초과**다(CLAUDE.md 규칙 6).

**승인 없이는 진행하지 않는다.** 기본값은 **범위 밖**으로 두고, 승인 시 배치 2 에 포함한다.

---

### 5-4. ⚠️ `GameBootstrapper.Setup.cs` 13건 — sink 등록 전에 실행되는가

**사전 정리는 "조합 루트라 sink 등록 전에 실행되는 로그가 있을 수 있다" 고 지적했다. 실제로 확인했다.**

#### 확인 결과 — **sink 등록 전에 실행되는 로그는 없다** (단, 단서 있음)

실측 근거 (`Bootstrap/GameBootstrapper.cs`):

| 줄 | 내용 |
|:-:|---|
| 442 | `private void Awake()` |
| **450** | **`LogSessionOwner.EnsureInitialized();`** ← sink 등록 지점 |
| 473 | `Start()` — 주석: *"다른 컴포넌트의 `Awake()`가 먼저 실행되도록 보장"* |
| 486 | `InitializeUnitStatsFromConfig();` ← `Setup.cs` 62·100행의 진입점 |
| 491 | `InitializeBuildingStatsFromConfig();` ← `Setup.cs` 126·235행의 진입점 |

**`Awake` 에서 sink 를 등록하고 `Start` 에서 `Setup.cs` 를 부른다.**
유니티 생명주기상 **같은 오브젝트의 `Awake` 는 `Start` 보다 항상 먼저 실행되므로,
`Start` 경로에서 나오는 로그는 sink 등록 후다.**

파일 상단 21행 주석도 이를 뒷받침한다 — *"`Awake()`가 아닌 `Start()`에서 초기화."*

#### ⚠️ 남은 확인 항목 (추정하지 않는다)

**위에서 진입점을 확인한 것은 `Setup.cs` 13건 중 4건(62·100·126·235행)이다.**
나머지 9건(564 · 787 · 797 · 810 · 832 · 871 · 880 · 886 · 893행 — 카메라 배치 · AI 초기화)의
호출 경로는 **이 Plan 시점에 전수 확인하지 않았다.**

→ **배치 2 착수 시 9건 각각의 호출 경로가 `Start` 이후인지 확인한 뒤 이관한다.**
`Awake` 경로에서 **`EnsureInitialized()`(450행)보다 앞서** 실행되는 자리가 발견되면,
그 자리는 `GameLog` 로 바꿔도 **sink 가 0개라 콘솔 폴백(1.8)만 타고 파일에 남지 않는다.**
그런 자리가 나오면 **임의 판단하지 않고 사용자에게 보고한다.**

> **참고:** 콘솔 폴백이 있어 로그가 **사라지지는 않는다.** 다만 **파일에 남지 않으므로
> 이 작업의 목적(§1-1)을 달성하지 못한다.** 그래서 "동작하니까 괜찮다" 로 넘기지 않는다.

---

## 6. 작업 방식과 배치

### 6-1. 방식 — **파일 단위로 판정과 이관을 동시에 한다**

**선행 task 는 3단계였다:** 기준 수립 → 전수 판정(별도 `LogAudit.md` **982줄**) → 이관.

**이번에는 별도 판정표를 만들지 않는다.**

| 선행 task 가 3단계였던 이유 | 지금 상태 |
|---|---|
| 판정 기준이 **없었다** — 그걸 만드는 것이 그 task 의 산출물이었다 | **`LogRules` 두 축 + 분류 원칙 1~4 + 원칙 간 우선순위가 확정되어 있다** |
| 이벤트 키 체계가 **없었다** | **`LogEvent` 32개가 존재한다** (§5-2) |
| 판정 사례가 **하나도 없었다** | **`LogAudit.md` 에 205건의 실제 판정 사례가 있다** — 애매하면 거기서 유사 사례를 찾는다 |

**즉 판정표는 기준을 만들기 위한 도구였고, 기준이 이미 있으면 중간 산출물일 뿐이다.**
982줄짜리 표를 다시 만들면 **그 표를 유지·검증하는 비용이 새로 생기고,
표와 코드가 어긋날 때 어느 쪽이 진실인지 다시 판단해야 한다**
(실제로 §5-3 ①에서 본 `LobbyManager` 누락 사고가 그 형태였다).

**대신 이렇게 한다:**
- 판정 근거는 **코드 주석이 아니라 커밋 단위로 남긴다** — 파일 하나를 끝낼 때마다 그 파일의 `개발`/`운영` 내역을 요약해 보고
- 애매한 건은 **표에 적어 두고 넘어가지 않고, 그 자리에서 사용자에게 확인**한다

### 6-2. 배치 — **검토 결과 사전 계획을 2곳 수정한다**

| 배치 | 대상 | 건수 | 파일 | 근거 |
|:-:|---|:-:|:-:|---|
| **1-A** | `Infrastructure/Network` — **상위 6파일** (`NetworkHealthSync` 14 · `RelayManager` 13 · `NetworkCombatController` 11 · `NetworkGameFlow` 10 · `NetworkUnitMovementController` 9 · `NetworkTileSync` 8) | 65 | 6 | 라이브 문제가 실제로 터지는 곳. §1-4 의 구멍이 여기 있다 |
| **1-B** | `Infrastructure/Network` — **나머지 8파일** (`ReconnectionHandler` 7 · `NetworkResourceSync` 6 · `NetworkGameManager` 4 · `UnityServicesInitializer` 3 · `NetworkUpgradeController` 3 · `NetworkUnit` 3 · `NetworkSkillController` 2 · `NetworkMistShrineController` 2) | 30 | 8 | 위와 같은 계층. **`NetworkGameManager`·`UnityServicesInitializer` 는 이미 부분 이관된 파일**이라 기존 `GameLog` 호출과 나란히 놓고 대조할 수 있다 |
| **2** | `Bootstrap` 26 + `Application` 9 + `Cloud` 8 + `Factories` 7 | 50 | 11 | 초기화·계정 계층. **§5-4 확인 항목**과 **§5-3 확인 사항 A·B** 가 여기 |
| **3** | `Presentation` (UI·오디오·입력·그리드) | 39 | 20 | 1~4건짜리 파일 20개. **누락 위험이 가장 큰 배치** |
| **4** | `Debug/UIManagerTestButtonHandler.cs` | 4 | 1 | §5-1 결정에 따라 **`Editor/` 16건은 빠지고 4건만 남는다** |
| | **합계** | **188** | **46** | `Editor/` 16건 / 4파일 제외 |

**각 배치마다 사용자 컴파일 확인을 받은 뒤 다음 배치로 넘어간다.**

#### ⚠️ 사전 배치 계획에서 바꾼 2가지와 그 근거

**변경 1 — 배치 1(95건 / 14파일)을 1-A / 1-B 로 쪼갠다.**

- **이유:** 95건은 **전체의 47%** 이고 파일이 14개다. 이걸 **한 번의 컴파일 확인으로 묶으면,
  실패했을 때 원인 후보가 14파일 95곳으로 벌어진다.**
- **특히 이 계층이 CS0234 위험이 가장 높다** — `Hexiege.Application` 네임스페이스 함정(§7-2)의
  실제 발생 이력 3건이 전부 이 근처였다.
- 6파일(65건) / 8파일(30건)로 나누면 **각 확인의 원인 범위가 절반 이하**가 된다.
- 사용자 확인 횟수가 1회 늘어나지만, **되돌아가는 왕복 1회의 비용이 확인 1회보다 크다.**

**변경 2 — 배치 4에서 `Editor/` 16건이 빠진다.**

- §5-1 결정에 따라 **`Editor/` 4파일 16건은 이관하지 않는다.**
- 사전 계획은 *"§3 의 제외 판정 결과에 따라 아예 없어질 수도 있다"* 고 적었으나,
  **`Debug/` 4건은 빌드에 포함되므로**(§5-1 ②) 배치 4가 통째로 없어지지는 않는다.
- 배치 4는 4건/1파일로 작아지므로, **배치 3과 함께 확인받아도 무방하다**(사용자 판단에 맡긴다).

#### 배치 순서는 유지하는 것이 맞다 — 검토 결과

사전 계획의 **순서 자체는 타당하다.** 근거:

- **배치 1 을 먼저 두는 것이 맞다.** §1-4 의 실제 구멍(`RelayManager`)이 여기 있고,
  선행 task 의 8파일과 같은 계층이라 **로그가 한 흐름으로 이어지는 이득이 즉시 발생**한다.
  가장 큰 배치를 뒤로 미루면 그 이득이 마지막까지 지연된다.
- **배치 3(Presentation)을 뒤에 두는 것이 맞다.** 1건짜리 파일이 14개라 **작업은 단순하지만 건당 이득이 가장 작다.**
- **배치 4를 마지막에 두는 것이 맞다.** 유일하게 "빌드에 포함되는 테스트 코드" 라 성격이 다르다.

---

## 7. 위험 요소

### 7-1. 🔴 `GameLog` 시그니처 혼동 — **선행 task 에서 실제로 4건 발생**

`GameLog` 는 축 B 에 따라 **두 개의 중첩 클래스**로 갈려 있고, 시그니처가 서로 다르다(실측).

| 클래스 | 메서드 | 시그니처 |
|---|---|---|
| `GameLog.Ops` | `Info` | `(LogEvent, string system, string className, string message, string data = null)` |
| `GameLog.Ops` | `Warn` | `(LogEvent, string, string, string, string data = null)` |
| `GameLog.Ops` | `Warn` **(예외)** | `(LogEvent, string, string, string, Exception, string data = null)` |
| `GameLog.Ops` | `Error` | `(LogEvent, string, string, string, string data = null)` |
| `GameLog.Ops` | `Error` **(예외)** | `(LogEvent, string, string, string, Exception, string data = null)` |
| `GameLog.Dev` | `Info` / `Warn` / `Error` | `(string system, string className, string message, string data = null)` — **`LogEvent` 인자 없음** |
| `GameLog.Dev` | `Error` **(예외)** | `(string, string, string, Exception, string data = null)` |

**위험 지점 3가지:**

1. **`Ops` 는 첫 인자가 `LogEvent`, `Dev` 는 아니다.** 축 판정을 바꾸면 **인자 개수가 통째로 밀린다.**
2. **예외 오버로드에서 `Exception` 자리와 `data` 자리가 인접해 있다.**
   선행 task 에서 **`Exception` 을 `string data` 자리에 넣는 실수가 4건** 발생했다.
   → **`e` 를 넘길 때는 반드시 `message` 다음 자리인지 확인한다.**
   `e.Message` 를 문자열로 눌러 담는 것도 금지다 — **1.9**: *"예외 타입이 텔레메트리 집계의 핵심 축"*.
3. **⚠️ 새로 발견한 함정 — `GameLog.Dev` 에는 `Warn` 의 `Exception` 오버로드가 없다.**
   `Ops.Warn` 에는 있는데 `Dev.Warn` 에는 없다(실측 확인).
   → **`개발` + `Warn` + 예외 객체** 조합을 만나면 **컴파일이 되지 않는다.**
   그때는 임의로 `Dev.Error` 로 올리거나 `e.Message` 로 눌러 담지 말고,
   **축 판정을 재검토한 뒤 사용자에게 보고한다.** (오버로드 추가는 이 task 범위 밖이다)

### 7-2. 네임스페이스 함정 — `Hexiege.Application`

프로젝트에 **`Hexiege.Application` 네임스페이스가 존재**하므로,
수식 없는 `Application` 은 **`UnityEngine.Application` 이 아니다.**

- **CS0234 실제 발생 이력 3건** — `LogcatCapture.cs` 에서 발생해 커밋 `9fcee6b7`(`UnityEngine.Application` 완전 수식)로 해소됨
- **이번 대상이 특히 위험한 이유:** 이관하면서 `using Hexiege.Application;`(= `GameLog` 위치)을
  **50파일에 새로 추가**하게 된다. 그 파일 중 `Application.persistentDataPath` 등을
  수식 없이 쓰던 곳이 있으면 **추가하는 순간 깨진다.**
- **대응:** `using` 추가 후 그 파일 안에 **수식 없는 `Application.` 사용처가 있는지 확인**하고,
  있으면 `UnityEngine.Application.` 으로 완전 수식한다.

### 7-3. 파일 50개 — 누락 위험

**이 작업의 가장 현실적인 실패 모드는 "컴파일 에러" 가 아니라 "조용한 누락" 이다.**

- 46파일(제외 후) 중 **1~4건짜리가 34파일**이다. 한 파일에서 1건을 빠뜨려도 **아무 증상이 없다.**
- **대응:** 각 배치 종료 시 **`grep` 재실행으로 잔존 건수를 숫자로 확인**한다.
  - 배치 1-A 후: `Infrastructure/Network` 잔존이 **95 → 30** 인지
  - 배치 1-B 후: `Infrastructure/Network` 잔존 **0** 인지
  - 배치 2 후: `Bootstrap` · `Application`(GameLog.cs·ILogSink.cs 제외) · `Cloud` · `Factories` 잔존 **0** 인지
  - 배치 3 후: `Presentation` 잔존 **0** 인지
  - 배치 4 후: 전체 잔존이 **234 → 46**(대상 아님 30 + `Editor/` 16)인지
- **최종 기대값: 46건.** 이 숫자가 안 맞으면 어딘가 누락되었거나 초과 수정된 것이다.

### 7-4. 중복 로깅 — `NetworkCombatController.cs`

`LogRules` **1.13** 이 이미 지목해 둔 자리가 배치 1-A 에 포함된다.

> `NetworkCombatController.cs` 의 raw `Debug.Log` — 잔존 **11건**.
> 그중 **128행(raw)이 새 역할 로그(141행)와 나란히 `IsServer` 를 찍는 중복**이 있다.

→ **1.14 금지 사항 9**(같은 사건 두 곳 로깅 금지)에 따라 **128행은 이관이 아니라 제거 후보**다.
다만 **기존 로직 제거는 WORKFLOW.md [4] 규칙에 따라 승인 대상**이므로,
**우선 주석 처리(비활성화)** 하고 근거를 보고한 뒤, 사용자 테스트 통과 후 최종 삭제한다.

### 7-5. 축 판정의 일관성 표류

**204건을 4~5개 배치에 나눠 처리하면, 배치마다 판정 기준이 미묘하게 흔들릴 수 있다.**

- 배치 1에서 `개발` 로 판정한 것과 같은 성격의 로그를 배치 3에서 `운영` 으로 판정하면,
  **서버 지표가 계층별로 들쭉날쭉해진다.**
- **대응:** 각 배치 시작 시 **`LogAudit.md` 의 유사 사례를 먼저 확인**하고,
  선행 판정과 다른 결론을 내릴 때는 **그 차이의 근거를 명시**한다.

### 7-6. 범위 밖 — 부수적으로 눈에 띌 수 있는 항목

`LogRules` **1.13** 이 "범위 밖으로 미룬 항목" 으로 기록해 둔 것이 이번 작업 중 눈에 띌 수 있다.

- **`FileSink.EditorLogsRootRelativeToAssets` 가 `private`** 이라 `LogcatCapture.cs` 가 경로 문자열을 복제하고 있다.
- **이번에도 범위 밖이다.** 눈에 띄어도 손대지 않고, 필요하면 보고만 한다. (CLAUDE.md 규칙 6)

---

## 8. 검증 방법

### 8-1. 배치마다 확인하는 것

| # | 확인 항목 | 방법 | 기대값 |
|:-:|---|---|---|
| 1 | **컴파일 통과** | 사용자 유니티에서 확인 | 에러 0. **특히 CS0234**(§7-2) |
| 2 | **잔존 건수** | `grep -rn "Debug\.Log" <해당 경로> --include=*.cs \| wc -l` | §7-3 의 배치별 기대값 |
| 3 | **`Unknown` 미사용** | `grep -rn "LogEvent.Unknown" Assets/_Project/Scripts` | **0건** (§5-2 기준 4) |
| 4 | **`Ops`/`Dev` 배분** | 그 배치의 `개발`/`운영` 건수를 보고 | 선행 실적(`개발` 58%)과 크게 다르면 판정 재검토 |
| 5 | **신규 키 유무** | 그 배치에서 추가한 `LogEvent` 멤버 목록을 보고 | 신설 시 **§5-2 기준 2 의 두 조건 충족 근거를 함께 제시** |

### 8-2. 전 배치 완료 후 (최종 확인)

| # | 확인 항목 | 방법 | 기대값 |
|:-:|---|---|---|
| 1 | **전체 잔존** | `grep -rn "Debug\.Log" Assets/_Project/Scripts --include=*.cs \| wc -l` | **46** (대상 아님 30 + `Editor/` 16) |
| 2 | **잔존 위치가 예상과 일치** | 위 결과를 파일별로 집계 | `GameLog.cs` 9 · `ILogSink.cs` 2 · `Infrastructure/Debug/` 19 · `Editor/` 16 **외 0건** |
| 3 | **민감 데이터 (1.6)** | 로그 파일에서 `@` 포함 줄 · 평문 UID/PlayerId 검색 | **0건** |
| 4 | **`[Conditional]` 오적용 (1.7)** | `GameLog.Ops` 에 `[Conditional]` 이 붙지 않았는지 | **미부착** |

### 8-3. 실기 확인 — 이 작업의 목적이 달성됐는지

**숫자만으로는 목적 달성을 증명하지 못한다.** §1-4 의 구멍이 실제로 메워졌는지 본다.

- 에디터에서 **랜덤매칭을 1회 수행**한 뒤 `_Logs/_editor/{오늘}/RuntimeLog.txt` 를 연다
- **확인 항목:** `[Network/RelayManager]` 계열 로그가 **파일에 실제로 기록되는가**
  (현재는 콘솔에만 나오고 파일에 0건이다 — 이 작업의 성패를 가르는 단일 지표)
- 이 확인은 **전 배치 완료 후 1회**면 충분하다. 배치마다 실기를 요구하지 않는다.

> **TC 작성 및 QA 요청은 하지 않는다.** WORKFLOW.md [5-1]·[5-3] 은
> *"사용자가 명시적으로 지시한 경우에만 진행"* 이라고 규정하고, **먼저 제안하는 것도 금지**한다.
> 위 8-3 은 [6] 사용자 테스트 단계에서 무엇을 보면 되는지를 적어 둔 것이다.

---

## 9. 범위 밖 (이번에 하지 않는 것)

| 항목 | 이유 |
|---|---|
| **`Editor/` 4파일 16건 이관** | §5-1 결정 — 빌드 미포함이라 서버 수집 대상이 될 수 없다 |
| **`LogRules` 개정** (§5-1 ④ 의 1.14 단서 명문화) | 규칙 문서 변경은 별도 작업. 후속 항목으로 분리 (CLAUDE.md 규칙 6) |
| **`PlayerProfileService` 삼킨 예외 5곳 신규 로그** | §5-3 ④ — **사용자 확인 사항 B.** 승인 시에만 배치 2에 포함 |
| **닉네임 마스킹 방식 변경** | §5-3 ③ — **사용자 확인 사항 A.** 승인 전까지 현행 표기 유지 |
| **`GameLog.Dev.Warn` 의 `Exception` 오버로드 추가** | §7-1 ③ — 로그 체계 자체의 변경. 필요해지면 보고만 한다 |
| **`FileSink.EditorLogsRootRelativeToAssets` 접근 수준 변경** | `LogRules` 1.13 이 범위 밖으로 기록해 둔 항목 (§7-6) |
| **`Lobby.unity` 직접 진입 커버** | `LogRules` 1.10 — **사용자가 커버하지 않기로 이미 결정**한 항목 |
| **로그 서버 전송 구현** | 이 작업은 전송의 **전제 조건**(구조화 필드 통일)을 갖추는 데까지다 |
| **로그 메시지 문구 개선 / 리팩토링** | 이관은 "어디에 기록하느냐" 만 바꾼다. 문구를 손대면 변경 범위가 불투명해진다 |
| **기존 로그 파일 소급 수정** | `_Logs/` 는 이력 기록. `LogRules` 1.4 가 소급 수정하지 않는다고 규정 |

---

## 10. 승인 요청 — 진행 전 확정이 필요한 3가지

| # | 항목 | Plan 의 제안 | 위치 |
|:-:|---|---|---|
| **A** | `PlayerProfileService` 닉네임 출력(118·147행) 처리 방식 | **판단 필요** — (a) 유지 / (b) 해시 / (c) 값 제거 중 선택 | §5-3 ③ |
| **B** | `PlayerProfileService` 삼킨 예외 5곳에 **`운영` 로그 신규 추가** | **기본값: 범위 밖.** 승인 시 배치 2 포함 | §5-3 ④ |
| **C** | `LogRules` **1.14 금지 사항 1** 에 에디터 전용 코드 예외 명문화 | **후속 task 로 분리** 제안 | §5-1 ④ |

**그리고 §5-1 의 핵심 결정 — `Editor/` 16건 제외 · `Debug/` 4건 포함 — 에 대한 승인이 필요하다.**

> **WORKFLOW.md [4]:** *"Plan.md 내용을 사용자에게 공유 → **명시적 승인 확인 후에만** 구현 시작"*
> **CLAUDE.md 규칙 11:** 코드 수정은 명시적 지시 없이는 허가를 받은 뒤에 진행.

---

# 11. 구현 결과 (2026-08-18 추가 · 전 배치 완료)

> **아래는 계획이 아니라 실제로 벌어진 일의 기록이다.** 위 §1~§10 은 착수 시점의 계획이므로 원문을 그대로 둔다.
> 숫자는 **전부 2026-08-18 재실측값**이며 추정이 없다(CLAUDE.md 규칙 10).

## 11-1. 무엇이 끝났는가 (자연어)

**게임이 실행 중에 남기는 기록이, 이제 콘솔 창이 아니라 파일에 남습니다.**

작업 전에는 로그의 절반만 파일에 남고 절반은 콘솔에만 찍히고 사라졌습니다. 그래서
"로그인은 됐는데 게임에 못 들어갔다" 같은 제보를 받아도, 로그 파일을 열면 **딱 그 구간만 비어** 있었습니다.
이번 작업으로 게임 코드 쪽 로그가 전부 파일에 남게 되어 **그 구멍이 메워졌습니다.**
코드가 하는 일은 하나도 바뀌지 않았고, "어디에 기록하느냐"만 바뀌었습니다.

## 11-2. 배치별 실적

| 배치 | 대상 | 이관 | `개발` | `운영` | 커밋 |
|:-:|---|:-:|:-:|:-:|---|
| **1-A** | `Infrastructure/Network` 상위 **6파일** | **65** | 35 | 29 | `3151d0df` |
| **1-B** | `Infrastructure/Network` 나머지 **8파일** | **23** | 17 | 6 | `793e584f` |
| **2** | `Bootstrap` · `Application` · `Cloud` · `Factories` **11파일** | **50** (신규 3 포함) | 45 | 8 | `3141bc61` |
| **3·4** | `Presentation` **20파일** + `Debug/` **1파일** | **43** | 42 | 1 | `5a554bc7` |
| — | `system` 문자열 교정 **16건** | — | — | — | `589c2fcf` |
| | **합계** | **181** | | | |

**선행 task 205건을 더해 누적 386건.**

- **1-B 가 계획 30건에서 23건으로 줄어든 이유**는 누락이 아니다. `NetworkGameManager` **4건**(666~718행 `/* */` 블록)과
  `UnityServicesInitializer` **3건**이 이관이 아니라 **주석 처리**로 정리되었다 — **30 = 23 + 7.**
- **배치 2 의 신규 3건**은 §5-3 ④ 의 사용자 승인 사항 B 다(아래 11-5).
- `LogEvent` enum 은 **36개**가 되었다 — **배치 2 에서 4개 신설, 나머지 배치는 신설 0건.**
  **`LogEvent.Unknown` 사용처는 여전히 0건**이다(§5-2 기준 4 유지).

## 11-3. 최종 잔존 — **46건. §8-2 의 기대값과 정확히 일치한다**

| 구분 | 건수 | 왜 남는가 |
|---|:-:|---|
| `Application/GameLog.cs` | **9** | **sink 0개일 때의 콘솔 폴백.** `LogRules` **1.8** 이 요구하는 동작 그 자체 |
| `Application/Interfaces/ILogSink.cs` | **2** | 주석 |
| `Infrastructure/Debug/` | **19** | 로그 시스템 본체 — `RuntimeLogger` 8 · `LogSessionOwner` 5 · `ConsoleSink` 5 · `FileSink` 1 |
| `Assets/Editor/` | **16** | §5-1 결정대로 이관하지 않았다. **규칙 예외가 아니라 「미적용 + 이유」** 로 `LogRules` **1.13** 에 기재했다(사용자 결정 — 나중에 에디트 모드 sink 를 만들면 할 수 있도록 규칙으로 막지 않는다) |

> **⚠️ `grep` 총계는 54 다.** `grep -rn "Debug\.Log" Assets/_Project/Scripts --include=*.cs` = **54**.
> 차액 **8건은 전부 주석**이다 — `NetworkGameManager` **4**(`/* */` 블록) ·
> `UnityServicesInitializer` **3**(`//` 로 비활성화한 2 + 산문 속 낱말 1) · `NetworkCombatController` **1**(§7-4 의 중복 로깅).
> **54 = 실제 호출 46 + 주석 8.**

## 11-4. ⭐ 중복 집계 해소 — **추정이 두 번 연속 빗나갔다**

§7-4 는 중복 1건만 예상했으나, 실제로는 **"중복처럼 보이지만 중복이 아닌" 자리**가 두 번 나왔다.

| 배치 | 상황 | 결과 |
|:-:|---|---|
| **1-B** | `RelaySetupFailed` 가 `RelayManager`(원인)와 `NetworkGameManager`(호출자) 양쪽에서 발생 | **3곳 중 2곳만** 같은 사안이었다. `Stage=CodeMissing` 은 **하위를 호출하지 않고 `return`** 해 대응 로그가 존재할 수 없다 → **`운영` 유지** |
| **3** | `PlayerProfileService` 를 부르는 View **4곳** | **4곳 중 3곳만** 같은 사안이었다. `ProfileView` 로그아웃은 `FirebaseAuthService.SignOutAsync` 에 **`catch` 가 없어** 하위 로그가 없다 → **`운영` 유지** |

**교훈: "같은 계층을 호출하니 중복일 것" 이라는 추정이 두 번 연속 틀렸다. 호출 경로를 끝까지 따라가야 한다.**
→ 이 교훈은 `LogRules` **1.14 금지 사항 9** 에 반영했다.

## 11-5. 사용자 확인 사항 A·B·C 의 처리 결과

| # | 항목 | 결과 |
|:-:|---|---|
| **A** | `PlayerProfileService` 닉네임 출력 | **(a) 그대로 둔다** 로 결정(사용자, 2026-08-18). 근거는 코드 주석에 남아 있다 — *"`LogRules` 1.6 이 규정한 세 항목(이메일 / UID·PlayerId / 토큰) 어디에도 해당하지 않고, 유저가 게임 안에서 스스로 공개하는 이름"*. 해당 로그는 현재 `GameLog.Dev.Info(... $"Nickname={nickname}#{code}")` 2곳 |
| **B** | 삼킨 예외 5곳에 **`운영` 로그 신규 추가** | **승인되어 배치 2 에 포함.** 로그 **3건**을 추가했다. `GetInt` / `GetBool` 은 `catch` 가 중첩이라 **바깥 `catch` 하나에만** 달았다 — **안쪽에만 달면 *"숫자 자리에 문자열"* 경로가 누락**되고, **양쪽에 달면 한 사건이 두 줄**이 되어 `LogRules` **1.14 금지 9** 를 어긴다 |
| **C** | `LogRules` **1.14 금지 1** 에 에디터 전용 코드 예외 명문화 | **명문화하지 않기로 확정.** 예외가 아니라 **「미적용 + 이유」** 로 **1.13** 에 적었다 — 에디터 도구는 플레이 모드 밖이라 sink 가 없어 **이관 효과가 원리적으로 0**일 뿐이고, 규칙으로 막으면 나중에 에디트 모드 sink 를 만들었을 때 되돌려야 한다 |

## 11-6. ⭐ `system` 문자열 교정 16건 — **폴더가 아니라 기능 기준이다** (커밋 `589c2fcf`)

배치 3 이 원본 `[Network]` 태그를 달고 있던 UI **4파일**의 `system` 을 **폴더 기준으로 `"UI"` 로 바꿨다가 되돌렸다.**

| 파일 | 건수 | 최종 `system` |
|---|:-:|:-:|
| `Presentation/UI/LobbyUI.cs` | 10 | `Network` |
| `Presentation/UI/NetworkStatusUI.cs` | 4 | `Network` |
| `Presentation/UI/GameEndUI.cs` | 1 | `Network` |
| `Presentation/UI/Views/Lobby/LobbyRootView.cs` | 1 | `Network` |
| | **16** | |

**근거 — `LogRules` 1.4 「카테고리 규칙」의 예시 셋이 전부 `Presentation` 클래스인데 System 이 갈린다:**
`[Combat/UnitView]`(`Presentation/Unit/`) · `[HexGrid/HexTileView]`(`Presentation/Grid/`) · `[UI/ProductionPanelUI]`(`Presentation/UI/`).
폴더 기준으로 통일하면 **매칭 실패를 추적할 때 `System=Network` 필터에 `LobbyUI` 의 방 참가 실패가 안 잡힌다.**

→ **예시만으로는 읽는 사람마다 갈렸고 실제로 갈렸으므로**, 이 해석을 `LogRules` **1.4 본문에 한 문장으로 명문화**했다.
**규칙 번호는 신설하지 않았다** — 코드 주석의 기존 참조와 어긋나기 때문이다.

## 11-7. ⚠️ 완료 옆에 붙는 단서 (규칙 10 — 과대 표기 금지)

1. **각 배치의 컴파일 통과는 사용자 유니티에서 확인되었으나, 실기 동작 테스트는 하지 않았다.**
   §8-3 이 정의한 확인(랜덤매칭 1회 후 `[Network/RelayManager]` 로그가 파일에 남는가)은 **아직 수행되지 않았다.**
2. **`LobbyUI` 가 현재 쓰이지 않을 가능성이 있다.** 로비가 `LobbyRootView` 계열로 재구성됐는데 `LobbyUI` 는 옛 구조 그대로다.
   **확인 없이 판단하지 않고 10건 전부 이관했다**(CLAUDE.md 규칙 10·12).
3. **`Assets/Editor/` 16건은 미적용이다.** 규칙 예외가 아니라 현행 구현의 한계다(11-3).
4. §9 의 나머지 범위 밖 항목(`FileSink.EditorLogsRootRelativeToAssets` 접근 수준 · 로그 서버 전송 · `Lobby.unity` 직접 진입)은
   **손대지 않았다.**

## 11-8. 변경 파일 리스트업 (WORKFLOW.md [12])

> **git 명령을 쓰지 않았다**(CLAUDE.md 규칙 5). 아래 목록은 **`GameLog` 호출 실측**으로 작성했고, 괄호 안은 현재 호출 건수다.

```
[수정] 배치 1-A — Infrastructure/Network (6파일)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkHealthSync.cs (14)
- Assets/_Project/Scripts/Infrastructure/Network/RelayManager.cs (13)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs (11)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs (10)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs (9)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkTileSync.cs (8)

[수정] 배치 1-B — Infrastructure/Network (8파일)
- Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs (7)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkResourceSync.cs (6)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs (42 — 선행 이관분 포함)
- Assets/_Project/Scripts/Infrastructure/Network/UnityServicesInitializer.cs (7 — 선행 이관분 포함)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs (3)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs (3)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs (2)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkMistShrineController.cs (2)

[수정] 배치 2 — Bootstrap (5파일)
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs (13)
- Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs (8)
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Network.cs (3)
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs (1)
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs (1)

[수정] 배치 2 — Application / Cloud / Factories (6파일)
- Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs (8)
- Assets/_Project/Scripts/Application/UseCases/UnitSpawnUseCase.cs (1)
- Assets/_Project/Scripts/Infrastructure/Cloud/PlayerProfileService.cs (8 — 이관 5 + 신규 3)
- Assets/_Project/Scripts/Infrastructure/Cloud/LeaderboardService.cs (3)
- Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs (5)
- Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs (2)

[수정] 배치 3 — Presentation (20파일)
- Assets/_Project/Scripts/Presentation/UI/LobbyUI.cs (10)
- Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs (4)
- Assets/_Project/Scripts/Presentation/UI/NetworkStatusUI.cs (4)
- Assets/_Project/Scripts/Presentation/UI/Common/ToastUI.cs (3)
- Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs (2)
- Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs (2)
- Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs (1)
- Assets/_Project/Scripts/Presentation/Input/InputHandler.cs (1)
- Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs (1)
- Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs (1)
- Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs (1)
- Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs (1)
- Assets/_Project/Scripts/Presentation/UI/MistShrinePanelUI.cs (1)
- Assets/_Project/Scripts/Presentation/UI/ResearchMatrixView.cs (1)
- Assets/_Project/Scripts/Presentation/UI/UIManager.cs (1)
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs (1)
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs (1)
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs (1)
- Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs (1)
- Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs (1)

[수정] 배치 4 — Debug (1파일)
- Assets/_Project/Scripts/Debug/UIManagerTestButtonHandler.cs (4)

[수정] 이벤트 키
- Assets/_Project/Scripts/Application/Interfaces/ILogSink.cs (LogEvent 32 → 36)

[수정 없음 — 명시]
- Assets/_Project/Scripts/Editor/ 4파일 (§5-1 결정 — 이관하지 않음)

[문서]
- Assets/_Project/Docs/_Tasks/2026-08-17/17_19_remaining-layers-log-migration/Plan.md (이 문서 §11 추가)
- Assets/_Project/Docs/LogRules.md (1.4 · 1.7 · 1.13 · 1.14)
```

**총 47파일 수정** (Network 14 + Bootstrap 5 + Application/Cloud/Factories 6 + Presentation 20 + Debug 1 + `ILogSink.cs` 1).
§6-2 의 계획 **46파일** 대비 1개 많은 것은 **`ILogSink.cs`(`LogEvent` 4개 신설)** 가 이관 대상 파일 수에 포함되지 않았기 때문이다.
