# LogAudit — 네트워크·인증 계층 로그 두 축 판정표 (1~3단계 산출물)

**작성일:** 2026-08-13
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-13/07_13_network-auth-log-cleanup/`
**상위 문서:** [Plan.md](Plan.md) **§0-6** — 이 문서는 Plan §0-3의 **1·2·3단계 산출물**이다
**선행 문서:** [Research.md](Research.md) — 실측·경계 사례 초안
**기준 문서:** `Assets/_Project/Docs/LogRules.md` — **판정 기준의 단일 소스**
**이 단계에서 코드는 한 줄도 수정하지 않았다.** 코드는 읽기만 했다.

---

## 이 문서가 무엇이고 왜 필요한가 (자연어 설명 — 기술 용어 없이)

게임의 **접속·방 만들기·로그인** 부분에는 개발자가 붙여 둔 **"지금 여기까지 왔다"는 쪽지**가 200장 넘게 있습니다.
앞선 논의에서 이 쪽지들을 **두 갈래 통로**로 옮기기로 정했습니다.

- **개발용 통로** — 출시본에서는 통로째로 사라집니다. 개발 중에만 보입니다.
- **운영용 통로** — 출시본에도 남습니다. 나중에 서버로 모아서 **"이 문제가 몇 명에게 몇 번 일어났나"** 를 셀 수 있게 이름표를 붙입니다.

**이 문서는 쪽지 한 장 한 장을 어느 통로로 보낼지 정한 결과표입니다.** 세 가지를 담고 있습니다.

1. **쪽지마다 두 가지 값을 매긴 표** — "얼마나 심각한가"(축 A)와 "출시본에 남길 것인가"(축 B)
2. **판단이 갈렸던 쪽지들을 확정 기준으로 다시 본 결과** — 대부분 근거를 갖고 갈렸고, 몇 건만 사용자 확인이 필요합니다
3. **출시본에 남기기로 한 쪽지에 붙일 이름표 목록** — 이름이 제각각이면 나중에 숫자를 셀 수 없으므로 미리 통일해 둡니다

**여기서 정한 것은 아직 코드에 반영되지 않았습니다.** 실제로 옮기는 일은 다음 단계입니다.

---

## 0. 왜 이 표를 별도 파일로 뺐는가 (배치 근거)

| 후보 | 판단 | 근거 |
|------|:-:|------|
| `Research.md` 에 넣기 | **기각** | Research는 **"현재 코드가 어떤 상태인가"** 를 조사한 문서다. 이 표는 조사 결과가 아니라 **기준을 적용해 내린 결정**이다. 성격이 다른 것을 같은 문서에 섞으면 *"이건 사실인가 결정인가"* 를 구분할 수 없게 된다 |
| `Plan.md` 본문에 넣기 | **기각** | 성격은 맞다(Plan §0-3의 1~3단계 산출물이다). 그러나 Plan.md는 이미 **781줄 / 64KB**이고, 205행 판정표를 본문에 넣으면 **"지금 유효한 계획"(§0)이 표에 파묻힌다.** 4단계 이관 작업자가 계획을 읽으려면 표를 지나쳐야 한다 |
| **별도 파일 `LogAudit.md`** | **채택** | **Plan은 "무엇을 어떤 순서로 할 것인가", 이 파일은 "각 줄을 어떻게 할 것인가"** 로 역할이 갈린다. 4단계 이관 작업자는 파일 하나를 열어 놓고 한 줄씩 대조하면 되므로 **작업 형태와도 맞는다.** 위치를 같은 task 폴더로 잡은 이유는 이 산출물이 **이 작업에 귀속된 이력**이기 때문이다(`WORKFLOW.md` 폴더 구조 — `_Tasks/YYYY-MM-DD/HH_MM_[작업명]/`) |

> **파일명 근거:** `Research` / `Plan` / `Testcase` 는 `WORKFLOW.md` 가 규정한 세 문서다.
> 이 파일은 그 셋 중 어느 것도 아니므로 **세 이름 중 하나를 재사용하지 않고** 내용 그대로 `LogAudit.md` 로 짓는다.
> **Plan.md §0-6 에서 이 파일을 가리키므로**, Plan만 읽고 이 표의 존재를 놓치는 일은 생기지 않는다.

---

## 1. 착수 전 재실측 — Research 기록(209)과 다르다

`Plan.md` **§3-0 착수 전 재실측 (필수 · 코드 무변경)** 에 따라 8개 파일을 다시 셌다.

### 1-1. 계수 방법 (재현 가능하도록 명시)

Research **§1-2 계수 방법** 의 `grep -cE` 는 **주석 안의 줄까지 함께 센다.**
이번에는 **블록 주석(`/* */`)과 줄 주석(`//`)을 제거한 뒤** `Debug.Log(` / `Debug.LogWarning(` / `Debug.LogError(` 출현 횟수를 셌다.
(스크립트는 임시 파일로 실행했고 리포지토리에 남기지 않았다.)

### 1-2. 실측 결과

| # | 파일 | 원시 출현 | **주석 제외 = 실행되는 건** | Log | Warn | Error |
|:-:|------|:-:|:-:|:-:|:-:|:-:|
| 1 | `Infrastructure/Network/NetworkGameManager.cs` | 45 | **41** | 24 | 2 | 15 |
| 2 | `Infrastructure/Network/NetworkBuildingController.cs` | 40 | **40** | 13 | 15 | 12 |
| 3 | `Infrastructure/Network/NetworkProductionController.cs` | 35 | **35** | 13 | 17 | 5 |
| 4 | `Infrastructure/Network/LobbyManager.cs` | 28 | **28** | 14 | 4 | 10 |
| 5 | `Infrastructure/Auth/FirebaseAuthService.cs` | 20 | **20** | 14 | 2 | 4 |
| 6 | `Application/UseCases/LoginUseCase.cs` | 18 | **18** | 8 | 7 | 3 |
| 7 | `Infrastructure/Network/NetworkGameEndController.cs` | 16 | **16** | 16 | 0 | 0 |
| 8 | `Infrastructure/Network/UnityServicesInitializer.cs` | 9 | **7** | 6 | 0 | 1 |
| | **합계** | **211** | **205** | **108** | **47** | **50** |

### 1-3. 세 숫자(211 / 209 / 205)의 관계 — 전부 설명된다

| 숫자 | 출처 | 무엇을 센 값인가 |
|:-:|------|------------------|
| **211** | 상위 에이전트 실측 | **원시 출현 횟수.** 주석 처리된 죽은 호출 6건을 포함한다 |
| **209** | Research **§2-4 파일별 실측 + 분류 결과** | 211에서 `UnityServicesInitializer.cs:113·115` **2건만** 뺀 값 |
| **205** | **이번 재실측 (확정값)** | 211에서 죽은 호출 **6건 전부**를 뺀 값 |

### 1-4. **정정 ③** — Research가 놓친 죽은 호출 4건 (신규 발견)

Research **§2-2 정정 ①** 은 죽은 호출을 **2건**으로 적었다. 실제로는 **6건**이다.

| 파일:라인 | 상태 | Research가 놓친 이유 |
|-----------|------|---------------------|
| `UnityServicesInitializer.cs:113` | `//` 줄 주석 | — (Research가 이미 기록) |
| `UnityServicesInitializer.cs:115` | `//` 줄 주석 | — (Research가 이미 기록) |
| **`NetworkGameManager.cs:583`** | **`/* */` 블록 주석 내부** | **줄 자체는 `//` 로 시작하지 않는다.** 블록 주석 범위(`563`~`615`)를 추적해야만 죽은 줄임을 알 수 있다 |
| **`NetworkGameManager.cs:593`** | **`/* */` 블록 주석 내부** | 위와 동일 |
| **`NetworkGameManager.cs:606`** | **`/* */` 블록 주석 내부** | 위와 동일 |
| **`NetworkGameManager.cs:611`** | **`/* */` 블록 주석 내부** | 위와 동일 |

**블록 주석의 정체 (코드에 명시되어 있다):**
`NetworkGameManager.cs:549~561` 주석이 *"[비활성화됨] 2026-07-17 — 구 매칭 클라이언트 참가 경로 (A방식으로 대체)"* 라고 적고,
*"즉시 삭제가 아니라 비활성화(주석)다. 사용자 실기 테스트 통과 후 별도 단계에서 최종 삭제한다 (WORKFLOW [4] 규칙)"* 라고 남겨 두었다.
→ **`WORKFLOW.md` [4] 기존 로직 제거 규칙이 정상적으로 지켜진 결과물**이며, 이번 로그 작업의 대상이 아니다.

### 1-5. 이 정정이 기존 문서에 미치는 영향

| 기존 기술 | 정정 후 |
|-----------|---------|
| Research **§2-4** 합계 **209** | **205** (표 자체는 이력으로 보존 — Research에 갱신 주석을 달았다) |
| Research **§5-B4 폴링 진행 로그 2건** (`:514`, `:583`) | **`:583`은 죽은 코드라 판정 대상이 아니다.** 살아 있는 경계는 `:514` **1건** |
| `Plan.md` **§3-3 [5단계]** 제거 대상에 포함된 `593`·`606` | **죽은 코드.** 이관 대상이 아니다 |
| `Plan.md` **§3-0** 기준값 `NetworkGameManager.cs 27/2/16 = 45` | **원시 출현 기준으로는 그대로 45.** 실행되는 건은 **41** |

> **`Debug.Log` 27 → 24 로 줄어 보이는 이유도 이것이다.** 블록 주석 안의 `Debug.Log` 3건(`583`·`593`·`606`)과
> `Debug.LogError` 1건(`611`)이 빠진 값이다. **코드는 변경되지 않았다.**

---

## 2. 판정 기준 (적용 순서) — `LogRules.md` 요약, 단일 소스는 그쪽이다

> **규정 본문을 이 문서에 복제하지 않는다. 충돌 시 언제나 `LogRules.md` 가 옳다.**

| 축 | 판정 질문 | 값 |
|----|-----------|-----|
| **A — 심각도** (`LogRules.md` **1.2 두 축 — 심각도와 존속**) | **"복구되었나?"** | `Error` 복구 경로 없음 / `Warn` 대체 경로로 계속 진행 / `Info` 의도된 흐름 |
| **B — 존속** (같은 절) | **둘 다 "예"** 여야 `운영`<br>① 플레이어 기기에서만 벌어지는가(개발자가 재현할 수 없는가)<br>② 이 로그가 없으면 원인 추적 수단이 없는가 | `운영` / `개발` / `임시` |

**분류 원칙 4가지** (`LogRules.md` **1.3 분류 원칙**) — 이 순서로 기계적으로 먼저 적용했다.

| # | 원칙 | 이 판정에서 실제로 갈랐던 자리 |
|:-:|------|------------------------------|
| **1** | 예외를 던지는 쪽은 로그하지 않는다 — 최종 처리 지점에서 **한 번만** | 호출부/피호출부 중복 **13건**을 `개발`로 내렸다 (§4-2 참조) |
| **2** | UI로 이미 알린 실패는 `개발`. **단 서버가 거부했는데 클라이언트가 몰랐던 경우는 `운영`** | **서버 거부 15건 전부를 `운영`으로 확정**했다 |
| **3** | `Error`는 항상 `운영`. 단 설정 오류는 `Warn` + `개발` | `Error` 53건 중 **50건이 `운영`.** 나머지 3건은 원칙 1과 충돌 → **질의 항목 Q-3** |
| **4** | 삼킨 예외(`catch { }`)는 반드시 `운영` | `catch` 후 폴백/무시 **7건**을 `운영`으로 확정했다 |

**임시 계층은 0건이다.** `RuntimeLogger` 직접 호출이 8개 파일에 **한 건도 없다**(Research §6-2 ④ 재확인).

### 표 읽는 법

| 열 | 내용 |
|----|------|
| **라인** | 재실측 시점(2026-08-13) 기준. **코드가 바뀌면 다시 확인해야 한다** |
| **현재 호출** | `Log` = `Debug.Log` / `Warn` = `Debug.LogWarning` / `Err` = `Debug.LogError` |
| **축 A** | 재판정 결과. **현재 호출 레벨과 다르면 `↑승격` / `↓하향` 을 붙였다** |
| **축 B** | `운영` = `GameLog.Ops.*` / `개발` = `GameLog.Dev.*` |
| **키** | 축 B가 `운영`인 건에만 부여. `개발`은 `—` (`LogRules.md` **1.5 이벤트 키 — LogEvent**) |
| **근거** | 어느 원칙·판정 질문으로 갈렸는지 |

> **마스킹 표기(🔒)** 는 `LogRules.md` **1.6 민감 데이터** 대상임을 뜻한다. **처리는 5단계**다.

---

# 3. [1단계] 파일별 판정표 — 205건 전수

**배열 순서는 `Plan.md` §3-1의 위험도 오름차순을 그대로 따른다** (§0-3의 4단계가 이 순서로 이관한다).

---

## 3-1. `Infrastructure/Network/NetworkGameEndController.cs` — 16건

**파일 성격:** `LogWarning`·`LogError`가 **0건**이고 전부 `Debug.Log`다. 실패 경로에 로그가 아예 없다.

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 101 | Log — 스폰. IsServer= | `Info` | 개발 | — | 진입 흔적. 축 B ① 아니오 — 에디터에서 그대로 재현된다 |
| 113 | Log — 서버 측 OnGameEnd 구독 완료 | `Info` | 개발 | — | 위와 동일. 구독 실패 시 이후 흐름이 멈춰 별도로 드러난다 |
| 173 | Log — 서버: 게임 종료 감지(승리팀·랜덤매칭) | `Info` | 개발 | — | **경계 B2.** 축 B ① 아니오 — 게임 종료는 재현 가능. §4-2 B2 참조 |
| 202 | Log — AnnounceWinnerClientRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프. 결과는 화면에 즉시 반영된다(원칙 2) |
| 234 | Log — ForceWin 호출(강제 승리 팀) | `Info` | 개발 | — | 의도된 흐름. 연결 끊김 자체는 `NetworkGameManager:128`이 담당 |
| 250 | Log — 포기 요청 전송 | `Info` | 개발 | — | 사용자가 확인 팝업을 거쳐 누른 조작이다 |
| 278 | Log — 포기 처리(포기자 ClientId·팀) | `Info` | 개발 | — | **경계 B3 → 확정.** 의도된 흐름이고 재현 가능하다 |
| 320 | Log — RequestRematchServerRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 343 | Log — 양측 재경기 동의. 즉시 재경기 시작 | `Info` | 개발 | — | 의도된 흐름. 결과가 씬 재로드로 드러난다 |
| 358 | Log — 재경기 요청 수신 | `Info` | 개발 | — | RPC 수신 덤프. 팝업이 화면에 뜬다(원칙 2) |
| 368 | Log — AcceptRematchServerRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 380 | Log — DeclineRematchServerRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 408 | Log — 재경기 거절 알림 수신 | `Info` | 개발 | — | RPC 수신 덤프. 거절 안내가 화면에 뜬다 |
| 443 | Log — StartRematch: 동적 NetworkObject Despawn | `Info` | 개발 | — | **루프 안.** `LogRules.md` **1.14 금지 사항** 8(매 틱·매 프레임 로깅 금지)에 걸린다. `Dev`면 릴리스에서 `netObj.name` 접근까지 통째로 사라진다(**1.7 릴리스 스트리핑**) |
| 455 | Log — StartRematch: Game 씬 재로드 | `Info` | 개발 | — | 의도된 흐름 |
| 471 | Log — NotifyRematchStartingClientRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프. 로딩 인디케이터가 화면에 뜬다 |

**소계:** `Info` 16 / `Warn` 0 / `Error` 0 · **개발 16 / 운영 0**

> **Research §10-③(실패 경로 로그가 없는 것이 의도인지)은 여전히 미확인이다.**
> 다만 **재판정 결과로 이 파일의 운영 로그가 0건이라는 사실이 확정**되었다 —
> 즉 **재경기·포기 경로에서 문제가 생기면 릴리스 빌드에 아무 기록도 남지 않는다.**
> 실패 분기를 추가할지는 **로그 작업이 아니라 코드 설계 판단**이므로 이번 범위 밖으로 둔다(CLAUDE.md 규칙 6).

---

## 3-2. `Infrastructure/Network/NetworkBuildingController.cs` — 40건

**파일 성격:** 서버 RPC가 클라이언트 요청을 검증·거부하는 자리가 많다. **원칙 2가 이 파일을 가장 많이 갈랐다.**

**선행 확인(추정이 아니라 코드로 확인한 사실):**
- `Presentation/UI/BuildingPlacementUI.cs:519` 가 **클라이언트에서 `CanAfford`로 골드를 미리 막는다.**
- `Presentation/UI/ProductionPanelUI.cs` 가 **최고 단계에서는 업그레이드 버튼을 숨긴다**(`canUpgrade` → `CanvasGroup.alpha=0`, `interactable=false`).
- `Presentation/Input/InputHandler.cs:322` 주석대로 **Castle은 클릭해도 패널이 열리지 않는다**(`CanShowActionPanel == false`).
→ 따라서 **서버가 이 사유들로 거부했다는 것은 클라이언트가 막지 못했다는 뜻**이고, `LogRules.md` **1.3 분류 원칙** 2의 단서(서버가 거부했는데 클라이언트가 몰랐던 경우는 `운영`)에 정확히 해당한다.

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 58 | Warn — GameServicesLocator에 IGameServices 미등록 | `Warn` | **운영** | `NetworkControllerSpawnedWithoutGameServices` | 아직 기능이 죽지는 않았고(요청 전) 스폰은 계속된다 → `Warn`. NGO 스폰 타이밍은 회선 상태에 좌우돼 플레이어 기기에서만 어긋날 수 있고 통지 경로가 없다 → 운영 |
| 61 | Log — 스폰. IsServer= | `Info` | 개발 | — | 진입 흔적 |
| 133 | Log — 건물 배치 요청 수신(인자 덤프) | `Info` | 개발 | — | RPC 수신 덤프. 조작할 때마다 출력된다 |
| 142 | Err — RequestBuildServerRpc: GameBootstrapper 없음 | `Error` | **운영** | `ServerRpcGameServicesMissing` | 원칙 3. `LogRules.md` **1.2** 조합표가 이 자리를 `Error`+`운영` 예시로 이미 들었다 |
| 152 | Err — RequestBuildServerRpc: UseCase가 null | `Error` | **운영** | `ServerRpcGameServicesMissing` | 위와 같은 사건 유형 → **같은 키**(원칙 1 — 키가 갈리면 집계가 쪼개진다) |
| 171 | Warn — 팀 불일치 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | 요청만 거부하고 게임은 계속 → `Warn`. 정상 클라이언트가 보낼 수 없는 요청이다(원칙 2 단서) |
| 182 | Warn — 골드 부족 | `Warn` | **운영** | `ServerRejectedInsufficientResource` | 클라이언트가 `CanAfford`로 이미 막았는데 서버가 거부 = **클라·서버 골드 상태 불일치**(원칙 2 단서) |
| 200 | Warn — 서버 측 건물 배치 실패(위치 오류) | `Warn` | **운영** | `ServerActionExecutionFailed` | 검증 통과 후 실행 실패 = 맵 상태 불일치. `"배치 위치 오류"`는 토스트 매핑이 없어 **플레이어에게 통지되지 않는다** |
| 209 | Log — 서버: 건물 배치 성공 | `Info` | 개발 | — | 성공은 화면에 즉시 반영된다(원칙 2) |
| 239 | Log — SpawnBuildingClientRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 246 | Err — SpawnBuildingClientRpc: GameBootstrapper 없음 | `Error` | **운영** | `ClientRpcGameServicesMissing` | 원칙 3. 서버 RPC와 **결과가 다르다**(게임 전체 마비 vs 동기화 누락) → 키를 나눈다 |
| 253 | Err — SpawnBuildingClientRpc: UseCase가 null | `Error` | **운영** | `ClientRpcGameServicesMissing` | 위와 같은 사건 유형 |
| 272 | Warn — PlaceBuildingWithId 실패 | `Error` **↑승격** | **운영** | `ClientStateSyncApplyFailed` | **재시도 경로가 없다.** 그 건물은 이 클라이언트에서 **영구히 누락**된다 → "복구되었나?" 아니오 → `Error`(원칙 3) |
| 276 | Log — 클라이언트: 건물 재생성 완료 | `Info` | 개발 | — | 성공은 화면에 나타난다 |
| 314 | Warn — 건물 배치 실패: {reason} (ClientRpc) | `Warn` | 개발 | — | **원칙 1.** 같은 거부를 서버 쪽(171·182·200 등)이 **더 상세히** 이미 남긴다. 양쪽을 `운영`으로 두면 **한 사건이 두 번 집계된다** |
| 360 | Log — 건물 업그레이드 요청 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 366 | Err — RequestUpgradeServerRpc: GameBootstrapper 없음 | `Error` | **운영** | `ServerRpcGameServicesMissing` | `LogRules.md` **1.2** 조합표 명시 예시 |
| 376 | Err — RequestUpgradeServerRpc: UseCase가 null | `Error` | **운영** | `ServerRpcGameServicesMissing` | 원칙 3 |
| 385 | Warn — 업그레이드 대상 건물 없음 | `Warn` | **운영** | `ServerRejectedTargetNotFound` | 클라이언트는 **존재하는 건물의 패널만** 열 수 있다 → 서버에 없다는 것은 상태 불일치다 |
| 394 | Warn — 업그레이드 소유권 불일치 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | 정상 클라이언트가 보낼 수 없는 요청 |
| 403 | Warn — 업그레이드 불가(최고 단계) | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | UI가 최고 단계에서 버튼을 숨기고 클릭 시 `CanUpgrade`로 재확인까지 한다 → 도달하면 불일치 |
| 412 | Warn — 업그레이드 골드 부족 | `Warn` | **운영** | `ServerRejectedInsufficientResource` | `LogRules.md` **1.2** 조합표가 **이 자리를 그대로 예시로 들었다** |
| 424 | Warn — 서버 업그레이드 실행 실패 | `Warn` | **운영** | `ServerActionExecutionFailed` | 검증 4단계를 모두 통과한 뒤 실행이 실패했다 = 서버 내부 상태 이상 |
| 432 | Log — 서버: 업그레이드 성공 | `Info` | 개발 | — | 화면에 즉시 반영 |
| 460 | Log — UpgradeBuildingClientRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 466 | Err — UpgradeBuildingClientRpc: GameBootstrapper 없음 | `Error` | **운영** | `ClientRpcGameServicesMissing` | 원칙 3 |
| 473 | Err — UpgradeBuildingClientRpc: UseCase가 null | `Error` | **운영** | `ClientRpcGameServicesMissing` | 원칙 3 |
| 490 | Warn — UpgradeBuildingWithId 실패 | `Error` **↑승격** | **운영** | `ClientStateSyncApplyFailed` | 272와 동일 — 재시도 없음, 클라 건물 상태가 영구히 갈린다 |
| 494 | Log — 클라이언트: 업그레이드 동기화 완료 | `Info` | 개발 | — | 화면에 반영 |
| 523 | Log — 건물 철거 요청 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 529 | Err — RequestDemolishServerRpc: GameBootstrapper 없음 | `Error` | **운영** | `ServerRpcGameServicesMissing` | 원칙 3 |
| 539 | Err — RequestDemolishServerRpc: UseCase가 null | `Error` | **운영** | `ServerRpcGameServicesMissing` | 원칙 3 |
| 548 | Warn — 철거 대상 건물 없음 | `Warn` | **운영** | `ServerRejectedTargetNotFound` | 385와 동일 근거 |
| 556 | Warn — Castle은 철거할 수 없습니다 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | **Castle은 클릭해도 패널이 열리지 않는다**(InputHandler) → 정상 클라이언트가 보낼 수 없는 요청. `GameSystemRules_Buildings.md` 건물 철거 시스템 규칙 1(철거 가능 범위)의 서버 측 강제 지점 |
| 565 | Warn — 철거 소유권 불일치 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | 394와 동일 근거 |
| 588 | Log — 서버: 건물 철거 처리(환불액) | `Info` | 개발 | — | 화면·골드에 즉시 반영 |
| 613 | Log — DemolishBuildingClientRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 619 | Err — DemolishBuildingClientRpc: GameBootstrapper 없음 | `Error` | **운영** | `ClientRpcGameServicesMissing` | 원칙 3 |
| 626 | Err — DemolishBuildingClientRpc: UseCase가 null | `Error` | **운영** | `ClientRpcGameServicesMissing` | 원칙 3 |
| 634 | Log — 클라이언트: 건물 철거 동기화 완료 | `Info` | 개발 | — | 화면에 반영 |

**소계:** `Info` 13 / `Warn` 13 / `Error` 14 · **개발 14 / 운영 26**
**레벨 변경 2건:** `272`·`490` — `Warn` → `Error` 승격

> **⚠️ 이관과 무관한 코드 관찰 (범위 밖 · 수정하지 않았다)**
> `BuildFailedClientRpc`(312~320)의 `reason` → `ToastKey` 매핑에 **`"골드 부족"` 하나뿐**이다.
> 서버가 보내는 나머지 사유(`"팀 불일치"`, `"소유권 불일치"`, `"건물 없음"`, `"최고 단계"`, `"철거 불가"`,
> `"배치 위치 오류"`, `"서버 초기화 오류"`, `"맵 로드 중"`, `"업그레이드 실패"`)는 **플레이어에게 아무것도 표시되지 않는다.**
> 이 사실은 위 판정에서 *"UI로 알렸는가"* 를 **아니오**로 만들어 `운영` 판정을 더 강하게 뒷받침한다.
> **UX 문제로서의 처리 여부는 별도 판단 대상이다**(CLAUDE.md 규칙 6).

---

## 3-3. `Infrastructure/Network/NetworkProductionController.cs` — 35건

**선행 확인(코드로 확인한 사실):**
- `Presentation/UI/ProductionPanelUI.cs:499` 가 **클라이언트에서 `CanAfford`로 골드를 미리 막는다.**
- 같은 파일 `:503` 이 **`HasPopulation`으로 인구를 미리 막는다.**
→ 서버 측 골드·인구 거부는 **원칙 2 단서(클라이언트가 몰랐던 서버 거부)** 에 해당한다.

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 72 | Warn — GameServicesLocator에 IGameServices 미등록 | `Warn` | **운영** | `NetworkControllerSpawnedWithoutGameServices` | `NetworkBuildingController.cs:58` 과 **같은 사건 유형 → 같은 키** |
| 75 | Log — 스폰. IsServer= | `Info` | 개발 | — | 진입 흔적 |
| 183 | Log — 서버 측 생산 이벤트 구독 완료 | `Info` | 개발 | — | 진입 흔적 |
| 281 | Log — 서버 유닛 생산 완료 | `Info` | 개발 | — | 유닛이 화면에 나타난다(원칙 2) |
| 321 | Log — 유닛 생산 큐 요청(인자 덤프) | `Info` | 개발 | — | RPC 수신 덤프 |
| 329 | Err — RequestEnqueueServerRpc: GameBootstrapper 없음 | `Error` | **운영** | `ServerRpcGameServicesMissing` | 원칙 3 |
| 340 | Err — RequestEnqueueServerRpc: UseCase가 null | `Error` | **운영** | `ServerRpcGameServicesMissing` | 원칙 3 |
| 357 | Warn — 팀 불일치 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | 정상 클라이언트가 보낼 수 없는 요청(원칙 2 단서) |
| 369 | Warn — 배럭 생산 상태 없음 | `Warn` | **운영** | `ServerRejectedTargetNotFound` | 클라이언트는 존재하는 배럭의 패널만 연다 → 상태 불일치 |
| 380 | Warn — 골드 부족 | `Warn` | **운영** | `ServerRejectedInsufficientResource` | 클라이언트가 `CanAfford`로 이미 막았다(원칙 2 단서) |
| 391 | Warn — 인구 부족 | `Warn` | **운영** | `ServerRejectedInsufficientResource` | 클라이언트가 `HasPopulation`으로 이미 막았다. 골드와 **같은 성격이라 같은 키**로 묶고 사유는 `key=value`로 남긴다 |
| 403 | Warn — EnqueueUnit 실패(큐 가득) | `Warn` | **운영** | `ServerRejectedInsufficientResource` | 큐 용량도 자원의 일종. 아래 관찰대로 **토스트가 뜨지 않아** 플레이어가 알지 못한다 |
| 408 | Log — 서버: 유닛 생산 큐 추가 성공 | `Info` | 개발 | — | 큐가 화면에 즉시 반영된다 |
| 454 | Log — SpawnUnitClientRpc 수신 | `Info` | 개발 | — | RPC 수신 덤프 |
| 459 | Err — SpawnUnitClientRpc: GameBootstrapper 없음 | `Error` | **운영** | `ClientRpcGameServicesMissing` | 원칙 3 |
| 468 | Err — SpawnUnitClientRpc: UnitSpawnUseCase가 null | `Error` | **운영** | `ClientRpcGameServicesMissing` | 원칙 3 |
| 484 | Warn — SpawnUnitWithId 실패 | `Error` **↑승격** | **운영** | `ClientStateSyncApplyFailed` | 재시도 경로가 없다. 그 유닛은 이 클라이언트에서 영구히 누락된다 → 복구 안 됨 |
| 498 | Warn — UnitView 초기화 지연. 재시도 대기 | `Warn` | 개발 | — | **재시도 코루틴으로 복구를 시도한다** → `Warn`. 재시도가 끝내 실패하면 `:539`가 남으므로 **축 B ② 아니오**(원칙 1 — 최종 지점은 539) |
| 511 | Log — 클라이언트: 유닛 데이터 재생성 완료 | `Info` | 개발 | — | 화면에 반영 |
| 534 | Log — RetryInitializeUnitView: 초기화 성공 | `Info` | 개발 | — | 복구 성공 통보. 실패 시에만 539가 남는다 |
| 539 | Err — RetryInitializeUnitView: maxWait 초과, 초기화 실패 | `Error` | **운영** | `UnitViewInitializeTimeout` | 재시도 소진 후 복구 경로 없음. **메시지에 조치 방법(Network Prefabs List 확인)까지 들어 있다** — 지우면 손해가 명백하다 |
| 578 | Log — 클라이언트: 생산 시작 동기화 | `Info` | 개발 | — | UI에 즉시 반영 |
| 699 | Warn — CancelSlotServerRpc: 팀 불일치 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | 원칙 2 단서 |
| 708 | Warn — CancelSlotServerRpc: UnitProductionUseCase가 null | `Error` **↑승격** | **운영** | `ServerRpcGameServicesMissing` | 329·340과 **같은 불변식 위반**인데 이 자리만 레벨이 낮다. 취소 요청이 통째로 무시되고 복구 경로가 없다 |
| 718 | Warn — CancelQueueAt 실패 | `Warn` | **운영** | `ServerActionExecutionFailed` | 요청만 거부되고 게임은 계속 → `Warn`. 클라이언트 UI는 취소 성공을 가정하므로 상태가 갈린다. 통지 경로 없음 |
| 722 | Log — 서버: 큐 슬롯 취소 성공 | `Info` | 개발 | — | UI에 즉시 반영 |
| 764 | Warn — SetRallyPointServerRpc: 팀 불일치 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | 원칙 2 단서 |
| 776 | Warn — SetRallyPointServerRpc: UseCase가 null | `Error` **↑승격** | **운영** | `ServerRpcGameServicesMissing` | 708과 동일 근거 |
| 788 | Log — 서버: 랠리포인트 설정 완료 | `Info` | 개발 | — | 마커가 화면에 표시된다 |
| 817 | Warn — ToggleAutoServerRpc: 팀 불일치 | `Warn` | **운영** | `ServerRejectedUnauthorizedRequest` | 원칙 2 단서 |
| 825 | Warn — ToggleAutoServerRpc: UseCase가 null | `Error` **↑승격** | **운영** | `ServerRpcGameServicesMissing` | 708과 동일 근거 |
| 834 | Warn — ToggleAutoProduction 실패 | `Warn` | **운영** | `ServerActionExecutionFailed` | 요청 거부 후 계속 진행. 클라이언트 토글 상태와 갈린다 |
| 841 | Log — 자동 생산 토글 완료 | `Info` | 개발 | — | UI에 즉시 반영 |
| 869 | Log — 클라이언트: 자동 생산 상태 동기화 | `Info` | 개발 | — | UI에 즉시 반영 |
| 904 | Warn — 유닛 생산 큐 추가 실패: {reason} (ClientRpc) | `Warn` | 개발 | — | **원칙 1.** 서버 쪽(357~403)이 같은 거부를 더 상세히 남긴다. 양쪽 `운영`은 이중 집계다 |

**소계:** `Info` 13 / `Warn` 13 / `Error` 9 · **개발 15 / 운영 20**
**레벨 변경 5건:** `484`·`708`·`776`·`825` → `Error` 승격, `484`는 동기화 실패 / 나머지 셋은 불변식 위반

> **⚠️ 이관과 무관한 코드 관찰 (범위 밖 · 수정하지 않았다)**
> `EnqueueFailedClientRpc`(904~912)는 `reason == "큐 가득"` 일 때 `ToastKey.ProductionQueueFull` 을 발행하는데,
> 서버는 `:404` 에서 **`"큐 추가 실패 (큐 가득 참)"`** 를 보낸다. **문자열이 일치하지 않아 이 토스트는 발행되지 않는다.**
> 이 사실은 `:403` 의 축 B 판정에서 *"UI로 알렸는가"* 를 **아니오**로 만들어 `운영` 판정을 뒷받침한다.
> **문자열 불일치 자체의 수정 여부는 별도 판단 대상이다**(CLAUDE.md 규칙 6).

---

## 3-4. `Infrastructure/Network/LobbyManager.cs` — 28건

**파일 성격:** `OnError` 통지 경로가 **0건**이다. 실패 시 `null`/`false`를 반환할 뿐이라 **로그가 유일한 실패 기록**이다(Research §3-2 O-3).

**선행 확인 — 살아 있는 호출처가 없는 메서드가 있다(신규 발견):**

| 메서드 | 살아 있는 호출처 | 해당 로그 라인 |
|--------|:-:|---|
| `GetLobbiesAsync` | **0건** | 217, 222 |
| `FindLobbyByMatchIdAsync` | **0건** — 유일한 호출부가 `NetworkGameManager.cs:576`(블록 주석 내부) | 256, 262, 272, 274, 280 |
| `JoinLobbyByIdAsync` | **0건** — 유일한 호출부가 `NetworkGameManager.cs:595`(블록 주석 내부) | 299, 304 |
| `QuickJoinAsync` | **0건** | 338, 343 |

**합계 11건이 "컴파일에는 포함되지만 어떤 경로로도 도달하지 않는" 상태다.**
→ **판정은 정상적으로 매긴다**(public API라 나중에 다시 호출될 수 있다). 다만 표에 **【호출처 0건】** 으로 표시한다.
**메서드 자체를 지울지는 로그 작업이 아니라 코드 정리 판단이므로 이번 범위 밖이다**(CLAUDE.md 규칙 6).

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 104 | Log — Lobby 생성 완료(이름·코드·ID) | `Info` | 개발 | — | **경계 B2.** 축 B ① 아니오 — 에디터에서 그대로 재현된다. §4-2 B2 참조 |
| 111 | Err — Lobby 생성 실패: e.Message | `Error` | **운영** | `LobbyServiceCallFailed` | `catch` → `return null`. **원인은 `e.Message`에만 있고 반환값에 담기지 않는다**(O-3) |
| 153 | Err — CreateOrJoinLobbyByMatchId: matchId 비어 있음 | `Error` | **운영** | `LobbyInvariantViolated` | Matchmaker가 준 값이 비어 있다 = 불변식 위반. 매칭 흐름이 여기서 죽는다 |
| 177 | Log — CreateOrJoin 완료(matchId·LobbyId·HostId·IsHost) | `Info` | 개발 | — | **경계 B2.** 역할 확정 사실은 `NetworkGameManager.cs:433`도 남긴다(원칙 1) |
| 184 | Err — CreateOrJoin 실패(matchId): e.Message | `Error` | **운영** | `MatchmakingLobbyJoinFailed` | `LogRules.md` **1.2** 조합표가 **이 자리를 그대로 예시로 들었다**(*"매칭 로비 생성/참가 실패"*). 호출부는 `null`만 받아 *"다시 시도해주세요"* 를 띄운다 |
| 217 | Log — Lobby 목록 조회 완료(총 N개) | `Info` | 개발 | — | 조회 성공 통보 【호출처 0건】 |
| 222 | Err — Lobby 목록 조회 실패: e.Message | `Error` | **운영** | `LobbyServiceCallFailed` | `catch` → 빈 리스트 반환. **삼킨 예외**(원칙 4) 【호출처 0건】 |
| 256 | Log — Lobby 전체 조회: N개 | `Info` | 개발 | — | 조사용 흔적 【호출처 0건】 |
| 262 | Log — `- 로비: {name}, matchId=` | `Info` | 개발 | — | **루프 안 전수 열거.** `LogRules.md` **1.14 금지 사항** 8 【호출처 0건】 |
| 272 | Log — 매칭 Lobby 발견! | `Info` | 개발 | — | 조사용 흔적 【호출처 0건】 |
| 274 | Log — `FirstOrDefault null — 비교 실패` | `Info` | 개발 | — | **`LogRules.md` 1.14 금지 사항 3(의미 없는 메시지) 위반.** LINQ 메서드 이름이 그대로 노출돼 있다 → **이관 시 문구를 다시 쓴다** 【호출처 0건】 |
| 280 | Warn — Lobby 검색 실패(matchId): e.Message | `Warn` | **운영** | `MatchmakingLobbyJoinFailed` | `catch` → `return null`(삼킨 예외 · 원칙 4). 매칭 경로라 184와 **같은 지표로 묶는다** 【호출처 0건】 |
| 299 | Log — Lobby 참가 완료 (ID) | `Info` | 개발 | — | 성공 통보 【호출처 0건】 |
| 304 | Err — Lobby 참가 실패 (ID): e.Message | `Error` | **운영** | `LobbyServiceCallFailed` | O-3 — 원인이 로그에만 있다 【호출처 0건】 |
| 319 | Log — Lobby 참가 완료 (코드) | `Info` | 개발 | — | 성공 통보 |
| 324 | Err — Lobby 참가 실패 (코드): e.Message | `Error` | **운영** | `LobbyServiceCallFailed` | O-3. 호출부(`NetworkGameManager.cs:288`)는 `null`만 받는다 |
| 338 | Log — 빠른 매칭 성공 | `Info` | 개발 | — | 성공 통보 【호출처 0건】 |
| 343 | Err — 빠른 매칭 실패: e.Message | `Error` | **운영** | `LobbyServiceCallFailed` | O-3 【호출처 0건】 |
| 361 | Err — UpdateRelayJoinCode: 현재 Lobby 없음 | `Error` | **운영** | `LobbyInvariantViolated` | Host가 Relay 코드를 기록하려는데 Lobby가 없다 = 불변식 위반. 이후 클라이언트가 영원히 폴링만 하게 된다 |
| 377 | Log — Lobby에 Relay Join Code 저장 완료 | `Info` | 개발 | — | 성공 통보 |
| 381 | Err — Relay Join Code 업데이트 실패: e.Message | `Error` | **운영** | `LobbyServiceCallFailed` | **`catch` 본문이 로그 한 줄뿐인 삼킨 예외**(원칙 4 · Research §8-2). 실패하면 클라이언트가 코드를 영원히 못 받는다 |
| 398 | Warn — RefreshCurrentLobby: 현재 Lobby 없음 | `Warn` | 개발 | — | 호출부가 `false`를 무시하고 폴링을 계속한다 → 복구 경로 있음. 로컬 상태 문제라 재현 가능(축 B ① 아니오) |
| 409 | Warn — Lobby 갱신 실패(id): e.Message | `Warn` | **운영** | `LobbyServiceCallFailed` | `catch` → `return false`(삼킨 예외 · 원칙 4). 폴링이 왜 실패하는지의 **유일한 단서**다 |
| 435 | Log — Lobby 삭제 완료 (Host 퇴장) | `Info` | 개발 | — | 의도된 흐름 |
| 441 | Log — Lobby 나가기 완료 (Guest 퇴장) | `Info` | 개발 | — | 의도된 흐름 |
| 446 | Err — Lobby 나가기 실패: e.Message | `Warn` **↓하향** | **운영** | `LobbyServiceCallFailed` | **`finally`가 로컬 상태를 초기화하고 게임은 계속된다** → "복구되었나?" 예 → `Warn`. 다만 서버에 유령 Lobby가 남아 이후 매칭에 영향을 주고, 플레이어는 알 수 없다(원칙 4) |
| 498 | Warn — Heartbeat 전송 실패: e.Message | `Warn` | **운영** | `LobbyHeartbeatFailed` | 다음 틱에 재시도되므로 `Warn`. **실패 시에만** 출력되므로 금지 사항 8(매 틱 로깅)에 걸리지 않는다. Heartbeat가 끊기면 Lobby가 만료돼 매칭이 조용히 깨진다 |
| 502 | Log — Heartbeat 코루틴 종료 | `Info` | 개발 | — | 의도된 흐름 |

**소계:** `Info` 14 / `Warn` 5 / `Error` 9 · **개발 15 / 운영 13**
**레벨 변경 1건:** `446` — `Error` → `Warn` 하향

---

## 3-5. `Infrastructure/Network/UnityServicesInitializer.cs` — 7건 (+ 죽은 주석 2건)

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 76 | Log — UGS 초기화 시작... | `Info` | 개발 | — | 진입 흔적 |
| 78 | Log — UGS 초기화 완료 | `Info` | 개발 | — | 성공 통보. 실패는 `:139`가 남긴다 |
| 82 | Log — UGS 이미 초기화됨, 스킵 | `Info` | 개발 | — | 의도된 멱등 경로 |
| 123 | Log — 기존 UGS 세션 보존. PlayerId= 🔒 | `Info` | 개발 | — | 의도된 흐름. **PlayerId 출력 — `LogRules.md` 1.6에 따라 해시 치환 필요**(에디터에서도 동일하게 적용) |
| 129 | Log — UGS 세션 없음 — 익명 로그인으로 폴백 | `Warn` **↑승격** | **운영** | `UgsSessionMissingAnonymousFallback` | **경계 B6 → 확정.** 폴백 경로로 계속 진행 → `Warn`. 릴리스에서 이 줄이 뜨면 **Login 씬이 만든 OIDC 세션이 유실돼 PlayerId가 통째로 바뀐 것**이고, 플레이어는 아무것도 통지받지 못한 채 멀티플레이 정체성이 달라진다 → 축 B ①②가 모두 "예" |
| 134 | Log — UGS 초기화 완료. SignedIn / PlayerId 🔒 | `Info` | 개발 | — | **경계 B6 → 확정.** 성공 통보라 축 B ① 아니오. **PlayerId 해시 치환 필요** |
| 139 | Err — UGS 초기화 실패: e.Message | `Error` | **운영** | `UnityServicesInitializeFailed` | 원칙 3. **지역·회선·프로젝트 설정에 따라 달라져 개발 기기에서 재현되지 않는다**(O-1). `e.Message`가 유일한 단서다 |

**죽은 주석 2건:** `113`·`115` — **이관 대상이 아니다.** 실행되지 않는 잔해이며 처리 여부는 4단계에서 별도로 판단한다.

**소계:** `Info` 5 / `Warn` 1 / `Error` 1 · **개발 5 / 운영 2**
**레벨 변경 1건:** `129` — `Info` → `Warn` 승격

---

## 3-6. `Application/UseCases/LoginUseCase.cs` — 18건

**선행 확인:** `BridgeToUGSAsync`(442~497)는 **내부에서 모든 `Exception`을 잡아 `false`를 반환한다**(`:490~496`).
따라서 이 메서드를 감싼 바깥 `catch`(`:143`·`:315`)에 도달하는 예외는 **`BridgeToUGSAsync`가 잡지 못한 다른 예외**다 → 중복이 아니다(원칙 1 통과).

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 117 | Log — 자동 로그인: 세션 없음 | `Info` | 개발 | — | 의도된 흐름(첫 실행·로그아웃 후) |
| 127 | Log — 자동 로그인: 이메일 미인증 → 인증 대기 | `Info` | 개발 | — | **경계 B6 → 확정.** 의도된 분기이고 미인증 계정으로 재현 가능하다. 화면이 인증 대기 뷰로 넘어간다(원칙 2) |
| 131 | Log — 자동 로그인: 세션 발견 (UID=) 🔒 | `Info` | 개발 | — | 의도된 흐름. **UID 해시 치환 필요** |
| 137 | Warn — 자동 로그인: UGS 미연결 | `Warn` | 개발 | — | **경계 B5 → 확정.** 원칙 1 — 실패 원인은 `:494` 한 곳에만 있고 이 줄은 사건의 **복제**다 |
| 143 | Err — 자동 로그인 중 UGS 브릿지 실패: e.Message | `Error` | **운영** | `UgsBridgeUnhandledException` | 원칙 3. `BridgeToUGSAsync`가 삼키지 못한 예외라 **여기가 최종 처리 지점**이다 |
| 166 | Warn — 익명 로그인: UGS 미연결 | `Warn` | 개발 | — | **경계 B5 → 확정.** 137과 동일 문구·동일 사건(원칙 1) |
| 195 | Warn — Google 로그인: UGS 미연결 | `Warn` | 개발 | — | **경계 B5 → 확정.** 원칙 1 |
| 225 | Log — 이메일 미인증 계정 — NeedsEmailVerification 반환 | `Info` | 개발 | — | **경계 B6 → 확정.** 127과 동일 성격. 호출자(View)가 인증 화면으로 이동한다 |
| 233 | Warn — 이메일 로그인: UGS 미연결 | `Warn` | 개발 | — | **경계 B5 → 확정.** 원칙 1 |
| 304 | Warn — 이메일 인증 완료: UGS 미연결 | `Warn` | 개발 | — | **경계 B5 → 확정.** 원칙 1 |
| 315 | Err — CheckEmailVerifiedAsync 중 UGS 브릿지 실패 | `Error` | **운영** | `UgsBridgeUnhandledException` | 143과 동일 근거 → **같은 키** |
| 389 | Warn — UGS SignOut 중 예외(무시): e.Message | `Warn` | **운영** | `UgsSignOutFailed` | **`catch` 본문이 로그 한 줄뿐인 삼킨 예외**(원칙 4 · Research §8-2). SignOut이 실패하면 다음 로그인에서 **이전 계정 세션이 남아 계정이 뒤바뀔 수 있다** |
| 446 | Err — BridgeToUGSAsync: firebaseUID가 비어 있음 | `Error` | **운영** | `UgsBridgeMissingFirebaseUid` | 호출부 계약 위반 = 불변식 위반. UGS 연결이 여기서 죽고 복구 경로가 없다(원칙 3) |
| 455 | Log — UGS 초기화 시작... | `Info` | 개발 | — | 진입 흔적 |
| 469 | Log — 익명 계정 — UGS 익명 로그인 수행 | `Info` | 개발 | — | 의도된 분기 |
| 478 | Log — 실계정 — Firebase ID Token으로 OIDC 브릿지 수행 | `Info` | 개발 | — | 의도된 분기. **토큰 값 자체는 출력하지 않는다 — 규정 준수 상태다** |
| 485 | Log — UGS 브릿지 완료. PlayerId= 🔒 | `Info` | 개발 | — | 성공 통보. **PlayerId 해시 치환 필요** |
| 494 | Warn — UGS 브릿지 실패(멀티플레이 제한): e.Message | `Warn` | **운영** | `UgsBridgeFailed` | **경계 B5의 집약 지점.** `catch` → `return false`(삼킨 예외 · 원칙 4). **로그인은 성공으로 처리되어 플레이어는 아무것도 통지받지 못한다**(`AuthSystemRules.md` 오류 처리 규칙 3). 실패 원인이 여기에만 있다 |

**소계:** `Info` 8 / `Warn` 7 / `Error` 3 · **개발 13 / 운영 5**
**레벨 변경:** 없음

> **경계 B5 처리 방침(권고 · 코드 변경 없음):** 5건(`137`·`166`·`195`·`233`·`304`)을 `Dev`로 내리면
> *"어느 로그인 경로에서 UGS가 끊겼는가"* 라는 정보가 릴리스에서 사라진다.
> **이는 `:494`에 `Path=AutoLogin|Anonymous|Google|Email|EmailVerified` 를 `key=value`로 붙이면 그대로 보존된다**
> (`LogRules.md` **1.4 형식** — *"`key=value`가 곧 전송 데이터"*). **집약해도 정보를 잃지 않는다.**
> `BridgeToUGSAsync`에 경로 인자를 추가하는 것은 **4단계 이관 시 함께 판단**한다.

---

## 3-7. `Infrastructure/Auth/FirebaseAuthService.cs` — 20건

**파일 성격:** 민감 데이터 출력이 가장 많다. **`ConvertException`(`:550`)이 모든 Firebase 인증 오류가 지나가는 단일 변환 지점**이라, 원칙 1이 요구하는 *"최종 처리 지점에서 한 번만"* 구조가 이미 갖춰져 있다.

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 107 | Err — Firebase 의존성 해결 실패: {status} | `Error` | **운영** | `FirebaseDependencyUnavailable` | 원칙 3. **Google Play 서비스 상태는 기기마다 다르다** — 개발 기기에서 재현되지 않는다(O-1). `status` 값이 유일한 단서다 |
| 115 | Log — 초기화 완료. 기존 세션(UID=, Anonymous=) 🔒 | `Info` | 개발 | — | 성공 통보. **UID 해시 치환 필요** |
| 121 | Err — 초기화 예외: e.Message | `Error` | **운영** | `FirebaseInitializeFailed` | `catch` → `return false`. **삼킨 예외**(원칙 4) — 호출부는 실패 사유를 받지 못한다 |
| 146 | Log — 익명 로그인 성공. UID= 🔒 | `Info` | 개발 | — | 성공 통보. **UID 해시 치환 필요** |
| 191 | Log — Google 로그인 성공. UID=, DisplayName= 🔒 | `Info` | 개발 | — | 성공 통보. **UID 해시 치환 필요.** `DisplayName`은 `LogRules.md` **1.6**에 규정이 없다 → **미확인**(§5-2 ③) |
| 210 | Warn — Google Play Games login is only available on Android | `Warn` | 개발 | — | **`#if !UNITY_ANDROID` 분기 안에 있어 안드로이드 빌드에는 애초에 컴파일되지 않는다.** 플랫폼 설정 성격이라 **원칙 3의 단서**(설정 오류 → `Warn` + `개발`)에 해당한다 |
| 220 | Err — GPGS 인증 실패: {signInStatus} | `Warn` **↓하향** | **운영** | `GooglePlayGamesAuthFailed` | 빈 문자열을 반환해 상위가 `AuthException`으로 바꾸고 **UI가 안내한다** → 대체 경로로 계속 진행 → `Warn`. 다만 **`signInStatus` 코드는 여기에만 있고** UI 문구에는 담기지 않는다(축 B ②) |
| 233 | Err — GPGS Server Auth Code 발급 실패 | `Warn` **↓하향** | **운영** | `GooglePlayGamesAuthFailed` | 220과 **같은 사건(Google 로그인 실패) → 같은 키.** 어느 단계였는지는 `key=value`로 남긴다 |
| 238 | Log — GPGS Server Auth Code 발급 성공 | `Info` | 개발 | — | 성공 통보 |
| 265 | Log — 이메일 로그인 성공. UID=, EmailVerified= 🔒 | `Info` | 개발 | — | 성공 통보. **UID 해시 치환 필요** |
| 289 | Log — 회원가입 성공. UID= 🔒 | `Info` | 개발 | — | 성공 통보. **UID 해시 치환 필요** |
| 318 | Log — 인증 메일 발송: {Email} 🔒 | `Info` | 개발 | — | 성공 통보. **이메일은 부분 마스킹도 금지 — 출력 자체를 없앤다**(`LogRules.md` **1.6**) |
| 345 | Log — 인증 완료 확인 결과: {verified} | `Info` | 개발 | — | 의도된 흐름. 식별자를 담지 않는다 |
| 365 | Log — 비밀번호 재설정 메일 발송: {email} 🔒 | `Info` | 개발 | — | **이메일 출력 제거 필수** |
| 399 | Log — Google 연동 성공. UID= 🔒 | `Info` | 개발 | — | 성공 통보. **UID 해시 치환 필요** |
| 424 | Log — 이메일 연동 성공. UID=, Email= 🔒🔒 | `Info` | 개발 | — | **UID와 이메일을 동시에 출력한다.** 이메일은 제거, UID는 해시 치환 |
| 448 | Log — 로그아웃 완료 | `Info` | 개발 | — | 의도된 흐름 |
| 463 | Log — Current user deleted. UID= 🔒 | `Info` | 개발 **(잠정)** | — | **경계 B6 — 질의 항목 Q-4.** 축 A는 확정(요청대로 삭제가 성공했으므로 의도된 흐름 = `Info`). 축 B는 ①이 "아니오"(개발자가 재현 가능)라 규칙상 `개발`이지만, **되돌릴 수 없는 조작이라 감사 기록 요구가 축 판정을 덮을 수 있다.** §4-3 Q-4 참조. **UID 해시 치환 필요** |
| 503 | Log — ID Token 발급 완료 (UGS OIDC 브릿지용) | `Info` | 개발 | — | **토큰 값을 출력하지 않는다 — 규정 준수 상태다** |
| 550 | Warn — {operation} 실패: {code} → {reason} (e.Message) | `Warn` | **운영** | `FirebaseAuthOperationFailed` | **모든 `FirebaseException`이 지나는 단일 변환 지점**(원칙 1의 이상적 형태). `AuthException`으로 변환돼 UI가 한국어 요약을 보여 주지만 **원본 `code`·`reason`은 여기에만 남는다**(축 B ②). 계정·회선 상태에 좌우돼 재현 불가(축 B ①) |

**소계:** `Info` 14 / `Warn` 4 / `Error` 2 · **개발 15 / 운영 5**
**레벨 변경 2건:** `220`·`233` — `Error` → `Warn` 하향
**민감 데이터 11건:** `115`·`131`(LoginUseCase)과 별개로 이 파일에만 `115`·`146`·`191`·`265`·`289`·`318`·`365`·`399`·`424`·`463` = **10건** (`424`는 UID·이메일 2종)

---

## 3-8. `Infrastructure/Network/NetworkGameManager.cs` — 41건 (죽은 4건 제외)

**파일 성격:** `OnError?.Invoke` 가 **25건**으로, 에러 로그와 사용자 통지가 쌍으로 배치돼 있다.
그래서 **원칙 1(중복 로깅 금지)과 원칙 2(UI로 알린 실패는 개발)가 가장 자주 걸리는 파일**이다.

**선행 확인 — 원인을 가진 로그가 어디 있는가:**

| 이 파일의 실패 로그 | 같은 사건의 원인 로그 | 그 파일이 이번 범위인가 |
|---|---|:-:|
| `:201` 초기화 실패 | `UnityServicesInitializer.cs:139` (`e.Message` 보유) | **범위 안** |
| `:233` Lobby 생성 실패 | `LobbyManager.cs:111` (`e.Message` 보유) | **범위 안** |
| `:288` Lobby 참가 실패 | `LobbyManager.cs:324` (`e.Message` 보유) | **범위 안** |
| `:223` Relay 할당 실패 | `RelayManager.cs:78`·`:83` (`e.Message` 보유) | **범위 밖** → 질의 Q-2 |
| `:309` Relay 참가 실패 | `RelayManager.cs:122`·`:127` (`e.Message` 보유) | **범위 밖** → 질의 Q-2 |

| 라인 | 현재 호출 | 축 A | 축 B | 키 | 판정 근거 |
|:-:|:-:|:-:|:-:|---|------|
| 128 | Log — 클라이언트 측 서버 연결 끊김 감지 | `Warn` **(잠정)** | 개발 | — | **경계 B3 — 질의 항목 Q-1.** 축 B는 확정(`OnServerDisconnected` → UI 통지 경로가 있고 이 줄에는 원인이 없다). **축 A는 확정 불가** — §4-3 Q-1 참조 |
| 191 | Log — NetworkGameManager: 초기화 시작 | `Info` | 개발 | — | 진입 흔적 |
| 196 | Log — 초기화 성공. PlayerId= 🔒 | `Info` | 개발 | — | 성공 통보. **PlayerId 해시 치환 필요** |
| 201 | Err — 초기화 실패: e.Message + OnError | `Error` | 개발 | — | **원칙 1 · 2.** `UnityServicesInitializer.cs:139`가 **같은 예외를 같은 문구로 이미 남긴다**(운영). 여기서는 `OnError`로 화면에도 알린다. **원칙 3과 충돌 → 질의 Q-3** |
| 216 | Log — HostGame 시작. 방 이름 | `Info` | 개발 | — | 진입 흔적 |
| 223 | Err — Relay 할당 실패 + OnError | `Error` | **운영 (잠정)** | `RelaySetupFailed` | 원칙 1을 그대로 적용하면 `개발`이지만 **원인 로그(`RelayManager`)가 이번 범위 밖이라 `GameLog`로 이관되지 않는다** → 질의 Q-2. **잠정적으로 `운영` 유지**(정보를 잃지 않는 쪽) |
| 233 | Err — Lobby 생성 실패 + OnError | `Error` | 개발 | — | 원칙 1 — `LobbyManager.cs:111`이 원인을 보유. **질의 Q-3** |
| 255 | Log — Host 게임 시작 완료. Lobby Code | `Info` | 개발 | — | **경계 B2 → 확정.** §4-2 B2 참조 |
| 260 | Err — HostGame 예외: e.Message + OnError | `Error` | **운영** | `GameSessionStartUnhandledException` | **최종 `catch`이며 고유한 예외다**(원칙 1 통과). 원칙 3 |
| 281 | Log — JoinGame 시작. Lobby Code | `Info` | 개발 | — | 진입 흔적 |
| 288 | Err — Lobby 참가 실패 + OnError | `Error` | 개발 | — | 원칙 1 — `LobbyManager.cs:324`가 원인을 보유. **질의 Q-3** |
| 299 | Err — Relay Join Code를 Lobby에서 찾을 수 없음 | `Error` | **운영** | `RelaySetupFailed` | **하위 계층에 대응 로그가 없다**(원칙 1 통과). Host의 코드 기록이 늦거나 실패한 상태 — 플레이어 기기에서만 벌어진다 |
| 309 | Err — Relay 참가 실패 + OnError | `Error` | **운영 (잠정)** | `RelaySetupFailed` | 223과 동일 — **질의 Q-2** |
| 324 | Log — Client 게임 참가 완료 | `Info` | 개발 | — | 성공 통보. 화면이 전환된다 |
| 329 | Err — JoinGame 예외: e.Message + OnError | `Error` | **운영** | `GameSessionStartUnhandledException` | 최종 `catch` · 고유 예외(원칙 3) |
| 341 | Log — Disconnect 시작 | `Info` | 개발 | — | 의도된 흐름(대칭 쌍) |
| 359 | Log — Disconnect 완료 | `Info` | 개발 | — | 의도된 흐름(대칭 쌍) |
| 381 | Log — 티켓 생성: {ticketId} | `Info` | 개발 | — | 성공 통보. `ticketId`는 `LogRules.md` **1.6**의 규정 항목이 아니다 |
| 386 | Log — 매칭 완료. MatchId | `Info` | 개발 | — | **경계 B2 → 확정.** §4-2 B2 참조 |
| 402 | Err — StartMatchmakingAsync 예외: e.Message | `Error` | **운영** | `MatchmakingUnhandledException` | **`catch` 본문이 로그 한 줄뿐이고 `OnError`도 없다**(Research §8-2). 매칭이 조용히 죽는다 — 원칙 4 |
| 433 | Log — CreateOrJoin 결과 역할 (Host/Client) | `Info` | 개발 | — | 의도된 분기. 역할은 이후 흐름으로 드러난다 |
| 479 | Log — 매칭 Host 게임 시작 완료. Lobby Code | `Info` | 개발 | — | **경계 B2 → 확정** |
| 484 | Err — 매칭 Host 시작 예외 + OnError | `Error` | **운영** | `GameSessionStartUnhandledException` | 최종 `catch` · 고유 예외(원칙 3) |
| 514 | Log — RelayJoinCode 대기 중... (i/max) | `Info` | 개발 | — | **경계 B4 → 확정.** 루프 안 진행 로그는 상태 **전이**가 아니다 — `LogRules.md` **1.14 금지 사항** 8 |
| 538 | Log — Client 게임 참가 완료 (매칭) | `Info` | 개발 | — | 성공 통보 |
| 543 | Err — 매칭 Client 참가 예외 + OnError | `Error` | **운영** | `GameSessionStartUnhandledException` | 최종 `catch` · 고유 예외 |
| 631 | Warn — 티켓 삭제 중 오류 (무시): e.Message | `Warn` | **운영** | `MatchmakingTicketDeleteFailed` | **`catch` 본문이 로그 한 줄뿐인 삼킨 예외**(원칙 4). 흐름은 계속되지만 티켓이 서버에 남아 이후 매칭을 방해할 수 있다 |
| 636 | Log — 매칭 취소 완료 | `Info` | 개발 | — | 의도된 흐름 |
| 667 | Warn — LoadGameScene: 서버가 아니므로 무시 | `Warn` | **운영** | `SceneLoadRequestedByNonServer` | 요청만 무시하고 계속 → `Warn`. **정상 흐름에서는 발생할 수 없는 불변식 위반**이고 통지 경로가 없다(O-2) |
| 671 | Log — LoadGameScene: Game 씬 로드 시작 | `Info` | 개발 | — | 의도된 흐름. 씬 전환이 화면에 보인다 |
| 692 | Log — Client 접속 감지 (clientId=) | `Info` | 개발 | — | **경계 B3 → 확정.** 의도된 흐름이고 에디터 2인 구성으로 재현 가능하다 |
| 700 | Log — OnAllPlayersReady 발행. 접속 수= | `Info` | 개발 | — | 의도된 흐름 |
| 713 | Err — StartNetworkHost: NetworkManager.Singleton이 null | `Error` | **운영** | `NetworkManagerSingletonMissing` | **불변식 위반**(O-2). `.claude/MEMORY.md`의 *"GameBootstrapper가 유일한 의존성 조합 루트"* 전제가 깨진 상태다 |
| 719 | Log — NetworkManager.StartHost() 성공 | `Info` | 개발 | — | 성공 통보. 이후 흐름(Heartbeat 시작·`OnHostStarted`)으로 확인된다 |
| 721 | Err — NetworkManager.StartHost() 실패 | `Error` | **운영** | `NetworkSessionStartFailed` | 호스트 시작이 실패하면 게임을 만들 수 없다 — 복구 경로 없음(원칙 3) |
| 734 | Err — StartNetworkClient: NetworkManager.Singleton이 null | `Error` | **운영** | `NetworkManagerSingletonMissing` | 713과 동일 사건 유형 → **같은 키** |
| 740 | Log — NetworkManager.StartClient() 성공 | `Info` | 개발 | — | 성공 통보 |
| 742 | Err — NetworkManager.StartClient() 실패 | `Error` | **운영** | `NetworkSessionStartFailed` | 721과 동일 사건 유형 → **같은 키** |
| 755 | Log — NetworkManager Shutdown 완료 | `Info` | 개발 | — | 의도된 흐름 |
| 803 | Log — Heartbeat 코루틴 시작 | `Info` | 개발 | — | 의도된 흐름(대칭 쌍). 실제 실패는 `LobbyManager.cs:498`이 잡는다 |
| 815 | Log — Heartbeat 코루틴 정지 | `Info` | 개발 | — | 의도된 흐름(대칭 쌍) |

**소계:** `Info` 23 / `Warn` 3 / `Error` 15 · **개발 27 / 운영 14**
**레벨 변경:** 없음

> **`Plan.md` §3-4 ②의 `if`/`else` 반전은 불필요해졌다 — 재판정으로 확정된다.**
> `718~721`(StartHost)·`739~742`(StartClient)는 **성공 쪽이 `Dev`, 실패 쪽이 `Ops`로 양쪽 다 살아남는다.**
> 구문을 반전할 이유가 없고, 본문만 `GameLog` 호출로 바꿔 쓰면 구문이 그대로 성립한다.
> **`LobbyManager.cs:271~274`의 `if`/`else`도 마찬가지다** — 두 줄 모두 `Dev`로 살아남아 §3-4 ①의 구문 통째 제거가 불필요하다.
> → **`Plan.md` §3-4의 특수 처리 2종은 이 재판정으로 소멸이 확정되었다.**

---

# 4. [1단계] 집계 · [2단계] 경계 재판정

## 4-1. 축 A × 축 B 교차 집계 — 205건

| | **운영** | **개발** | **임시** | 합계 |
|---|:-:|:-:|:-:|:-:|
| **`Error`** | **50** | **3** | 0 | **53** |
| **`Warn`** | **35** | **11** | 0 | **46** |
| **`Info`** | **0** | **106** | 0 | **106** |
| **합계** | **85** | **120** | **0** | **205** |

**읽는 법 — 이 표가 말해 주는 것 네 가지**

1. **`Info` + `운영`이 0건이다.** 운영으로 남길 만한 사건은 전부 `Warn` 이상이었다.
   → `LogRules.md` 1.14 금지 사항 6(운영 로그에 이벤트 키 생략 금지)이 걸리는 자리가 정보 레벨에는 없다.
2. **`Error` 53건 중 50건이 `운영`이다.** 원칙 3(`Error`는 항상 `운영`)이 거의 그대로 관철되었다.
   나머지 3건은 원칙 1과 충돌한 자리이며 **질의 항목 Q-3**이다.
3. **`임시`가 0건이다.** 8개 파일에 `RuntimeLogger` 직접 호출이 한 건도 없다(Research §6-2 ④ 재확인).
4. **개발 120건은 릴리스에서 통째로 사라진다.** `LogRules.md` **1.7 릴리스 스트리핑**의 `[Conditional]` 2개가
   호출과 인자 평가까지 제거하므로, **문자열 보간 비용이 0이 된다** — Research §7-3이 우려한 구조적 비용이 여기서 해소된다.

### 파일별 집계

| 파일 | 계 | `Error` | `Warn` | `Info` | **운영** | **개발** | 레벨 변경 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `NetworkGameManager.cs` | 41 | 15 | 3 | 23 | 14 | 27 | 0 |
| `NetworkBuildingController.cs` | 40 | 14 | 13 | 13 | **26** | 14 | 2 ↑ |
| `NetworkProductionController.cs` | 35 | 9 | 13 | 13 | 20 | 15 | 4 ↑ |
| `LobbyManager.cs` | 28 | 9 | 5 | 14 | 13 | 15 | 1 ↓ |
| `FirebaseAuthService.cs` | 20 | 2 | 4 | 14 | 5 | 15 | 2 ↓ |
| `LoginUseCase.cs` | 18 | 3 | 7 | 8 | 5 | 13 | 0 |
| `NetworkGameEndController.cs` | 16 | 0 | 0 | 16 | **0** | 16 | 0 |
| `UnityServicesInitializer.cs` | 7 | 1 | 1 | 5 | 2 | 5 | 1 ↑ |
| **합계** | **205** | **53** | **46** | **106** | **85** | **120** | **10** |

### 축 A 레벨이 현재 호출과 달라진 10건

**축 A는 새로 매기는 값이라 현재 출력 함수와 어긋나는 것이 정상이다**(Research §2-4 갱신 주석 — *"레벨은 출력 함수일 뿐 판정을 거친 값이 아니다"*).

| 방향 | 건 | 사유 |
|:-:|---|------|
| **↑ `Warn` → `Error`** (6) | `NetworkBuildingController.cs:272`·`490`<br>`NetworkProductionController.cs:484`·`708`·`776`·`825` | 앞 3건은 **클라이언트 동기화가 재시도 없이 영구히 갈린다**, 뒤 3건은 **조합 루트 부재라는 같은 불변식 위반인데 이 자리만 레벨이 낮았다** |
| **↑ `Info` → `Warn`** (1) | `UnityServicesInitializer.cs:129` | **익명 폴백은 "의도된 흐름"이 아니라 "대체 경로"** 다 |
| **↓ `Error` → `Warn`** (3) | `LobbyManager.cs:446`<br>`FirebaseAuthService.cs:220`·`233` | 셋 다 **대체 경로로 흐름이 계속된다** — `finally` 상태 초기화 / `AuthException` 변환 후 UI 안내 |

> **레벨 변경은 4단계에서 `GameLog.Ops.Error(...)` / `GameLog.Ops.Warn(...)` 중 어느 메서드를 부르는지로 반영된다.**
> **별도의 코드 로직 변경이 아니다.**

---

## 4-2. [2단계] 경계 38건 재판정 — 확정 35 / 질의 2 / 대상 제외 1

Research **§5 경계 사례 — 판단이 갈리는 38건** 을 확정 기준으로 다시 보았다.

| 유형 | 건수 | 재판정 결과 | 갈린 근거 |
|------|:-:|---|---|
| **B1** 게임플레이 정상 거부 | 17 | **전건 확정** — `운영` 15 / `개발` 2 | **분류 원칙 2** |
| **B2** 상태 전이 성공 로그 | 6 | **전건 확정** — `개발` 6 | **축 B ①** |
| **B3** 접속·종료 감지 | 3 | **확정 2 / 질의 1** | 축 B ① · **Q-1** |
| **B4** 폴링 진행 | 2 | **확정 1 / 대상 제외 1** | **금지 사항 8** · 죽은 코드 |
| **B5** 동일 문구 5회 중복 | 5 | **전건 확정** — `개발` 5 | **분류 원칙 1** |
| **B6** 인증 분기·비가역 조작 | 5 | **확정 4 / 질의 1** | 축 B ①② · **Q-4** |
| **합계** | **38** | **확정 35 / 질의 2 / 제외 1** | |

### B1 (17건) — **원칙 2가 정면으로 갈랐다. 원안의 "제거 논거"는 폐기된다**

Research §5-B1의 제거 논거였던 ~~*"골드 부족·인구 부족은 UI가 이미 막고 있어야 하는 정상 경로라 로그로 남길 일이 아니다"*~~ 는 **성립하지 않는다.**
`LogRules.md` **1.3 분류 원칙** 2의 단서가 정확히 반대로 규정한다 — **"클라이언트가 미리 막았는데도 서버가 거부했다면 그것은 클라·서버 상태 불일치이고, 화면에 뜬 문구로는 절대 알 수 없는 정보"** 다.
게다가 `LogRules.md` **1.2 두 축 — 심각도와 존속** 의 조합표는 **`NetworkBuildingController.cs` 업그레이드 골드 검증을 `Warn` + `운영` 예시로 이미 넣어 두었다.**

**코드로 확인한 사실(추정 아님):** 클라이언트가 골드(`BuildingPlacementUI.cs:519`, `ProductionPanelUI.cs:499`)와
인구(`ProductionPanelUI.cs:503`)를 **전송 전에 이미 막는다.** 최고 단계는 버튼이 숨겨지고, Castle은 패널이 열리지 않는다.

| 분류 | 라인 | 판정 |
|---|---|:-:|
| **서버 측 거부 15건** | `NetworkProductionController.cs` 357·369·380·391·403·699·764·817<br>`NetworkBuildingController.cs` 171·182·394·403·412·556·565 | **전부 `운영`** |
| **클라이언트 측 통보 2건** | `NetworkBuildingController.cs:314` · `NetworkProductionController.cs:904` | **`개발`** — 원칙 1(같은 사건을 서버가 더 상세히 남긴다. 양쪽 `운영`은 이중 집계) |

> **원안 질문 ①전량 제거 / ②전량 유지 / ③골드·인구만 제거 는 셋 다 답이 아니다.**
> 확정 답은 **"서버 측 거부는 전부 `운영`, 클라이언트 측 통보만 `개발`"** 이다. **사용자 질의 불필요.**

### B2 (6건) — **전건 `개발`. Plan §6 갱신 주석의 "운영 쪽으로 기운다"는 판단을 뒤집는다**

| 라인 | 판정 |
|---|:-:|
| `NetworkGameManager.cs` 255 · 386 · 479 | `Info` / **개발** |
| `LobbyManager.cs` 104 · 177 | `Info` / **개발** |
| `NetworkGameEndController.cs:173` | `Info` / **개발** |

**갈린 근거 — 축 B ①이 "아니오"다.** *"플레이어 기기에서만 벌어지는가(개발자가 재현할 수 없는가)"* 에 대해
**방을 만들고 매칭이 성사되는 일은 에디터에서 그대로 재현된다.** 두 질문 **모두** "예"여야 `운영`이므로 여기서 끝난다.

**"MatchId가 특정 경기를 찾는 유일한 열쇠"라는 유지 논거는 어떻게 되는가:**
그 열쇠는 **성공 로그가 아니라 실패 로그에 붙어 있어야 쓸모가 있다.** 실제로 `LobbyManager.cs:184`는
이미 `matchId={matchId}` 를 실패 메시지에 담고 있고, 그 줄은 `운영`으로 확정되었다.
→ **`LogRules.md` 1.4 형식**의 `key=value` 로 **운영 로그 쪽에 식별자를 붙이는 것**이 규정에 맞는 해법이다.

> **⚠️ 다만 이 재판정은 남는 공백을 하나 만든다 — 관찰로 기록한다.**
> 경기 **도중**에 발생하는 운영 로그(건물·생산 거부 등)에는 **경기를 특정할 식별자가 없다.**
> 이를 해결하려면 `GameLog`에 세션 범위 공통 필드(예: `MatchId`)를 두는 구조가 필요한데,
> 그것은 **`GameLog` 설계 변경**이라 이번 1~3단계의 범위를 넘는다(CLAUDE.md 규칙 6).
> **추정으로 처리하지 않고 후속 판단 항목으로 남긴다.**

### B3 (3건) — 확정 2 / 질의 1

| 라인 | 판정 | 근거 |
|---|:-:|---|
| `NetworkGameManager.cs:692` (Client 접속 감지) | `Info` / **개발** — 확정 | 축 B ① 아니오. 에디터 2인 구성으로 재현 가능 |
| `NetworkGameEndController.cs:278` (포기 처리) | `Info` / **개발** — 확정 | 축 B ① 아니오. 의도된 조작이다 |
| `NetworkGameManager.cs:128` (서버 연결 끊김 감지) | **축 B = 개발 확정 / 축 A 미확정** | **질의 Q-1** |

### B4 (2건) — 확정 1 / 대상 제외 1

| 라인 | 판정 | 근거 |
|---|:-:|---|
| `NetworkGameManager.cs:514` (RelayJoinCode 대기 중) | `Info` / **개발** — 확정 | `LogRules.md` **1.14 금지 사항** 8이 직접 답한다. **루프 안 진행 로그는 상태 전이가 아니다** |
| ~~`NetworkGameManager.cs:583` (Lobby 대기 중)~~ | **판정 대상 아님** | **블록 주석(`563`~`615`) 내부의 죽은 코드다**(§1-4). 원안의 *"재시도 소진 시 1회만으로 바꿀까"* 라는 질문도 **함께 소멸한다** |

### B5 (5건) — 전건 `개발` 확정

`LoginUseCase.cs` **137 · 166 · 195 · 233 · 304** → 전부 `Warn` / **개발**.

**갈린 근거 — 분류 원칙 1.** `LogRules.md` **1.14 금지 사항** 9(*"같은 사건을 두 곳에서 로깅 금지 — 최종 처리 지점에서 한 번만"*).
다섯 줄은 **문구까지 동일**하고, 실패 원인(`e.Message`)은 `BridgeToUGSAsync` 내부 `:494` 에만 있다.
→ **`:494`가 최종 처리 지점**이고 그 줄이 `운영`이다.

> **원안 질문 *"집약하면 어느 경로에서 실패했는지가 사라진다"* 는 해소된다.**
> `:494`에 `Path=AutoLogin|Anonymous|Google|Email|EmailVerified` 를 `key=value`로 붙이면 정보가 그대로 남는다
> (`LogRules.md` **1.4 형식** — *"`key=value`가 곧 전송 데이터"*). **사용자 질의 불필요.**
> 인자 추가 자체는 코드 변경이므로 **4단계에서 함께 처리**한다.

### B6 (5건) — 확정 4 / 질의 1

| 라인 | 판정 | 근거 |
|---|:-:|---|
| `LoginUseCase.cs:127` · `:225` (이메일 미인증) | `Info` / **개발** — 확정 | 축 B ① 아니오. 화면이 인증 대기 뷰로 전환된다(원칙 2) |
| `UnityServicesInitializer.cs:129` (익명 폴백) | `Warn` **↑** / **운영** — 확정 | 축 B ①② **모두 예.** 릴리스에서 이 줄이 뜨면 OIDC 세션이 유실돼 PlayerId가 바뀌고, **플레이어는 통지받지 못한다** |
| `UnityServicesInitializer.cs:134` (PlayerId 출력) | `Info` / **개발** — 확정 | 성공 통보라 축 B ① 아니오. **마스킹은 별개로 필수**(1.6은 에디터에도 적용된다) |
| `FirebaseAuthService.cs:463` (계정 삭제) | 축 A `Info` 확정 / **축 B 잠정 개발** | **질의 Q-4** |

> **원안 질문 *"`Debug.LogWarning`으로 승격할까요"* 와 *"식별자를 마스킹할까요"* 는 둘 다 소멸했다.**
> 승격 여부는 **축 A의 판정 질문("복구되었나?")** 이 기계적으로 답하고,
> 마스킹은 **`LogRules.md` 1.6이 이미 확정 규정**이라 선택지가 아니다(UID는 해시 치환, 이메일은 출력 금지).

---

## 4-3. 사용자 질의 항목 — 4항목 / 7건 → **[2026-08-17] 전건 확정됨**

> **모두 "판정 근거가 없어서"가 아니라 "근거끼리 충돌하거나, 규칙 밖 요구가 개입해서" 남은 것이다.**
> 각 항목에 **잠정 판정과 그 이유**를 함께 적었다 — 답을 받기 전에도 4단계가 멈추지 않도록.

> ## ✅ [2026-08-17 갱신] 사용자 결정 — **잠정 판정을 그대로 확정한다**
>
> 사용자가 **Q-1 ~ Q-4 전 7건에 대해 "잠정 판정을 그대로 확정한다"** 고 결정했다.
>
> | # | 확정 판정 | 잠정 판정과 차이 |
> |:-:|---|---|
> | **Q-1** | `Warn` / `개발` | **없음** |
> | **Q-2** | `Error` / `운영` 유지 | **없음** |
> | **Q-3** | `Error` / `개발` | **없음** |
> | **Q-4** | `Info` / `개발` + UID 해시 치환 | **없음** |
>
> **판정 내용이 한 건도 바뀌지 않았으므로 코드 변경이 필요 없다.**
> 4단계 이관은 잠정 판정대로 수행됐고, 각 자리에 근거 주석도 이미 달려 있다.
>
> **아래 Q-1 ~ Q-4 본문의 질문·잠정 판정 서술은 원문 그대로 보존한다** —
> *"그때 왜 물었고 무엇이 충돌했는지"* 가 확정 판정의 근거이기 때문이다.
> 각 항목 끝의 **"잠정 판정: …"** 은 이제 **확정 판정**으로 읽는다.
>
> **⚠️ 코드 주석을 "잠정 → 확정"으로 고치는 작업은 2026-08-17 문서 갱신의 범위가 아니다** —
> 하지 않았다는 사실만 기록해 둔다(CLAUDE.md 규칙 6). `Plan.md` **§0-6 ⑤** 참조.

### Q-1. `NetworkGameManager.cs:128` — 정상 종료와 장애를 코드가 구분하지 못한다 (1건)

**코드로 확인한 사실:** `DisconnectAsync`(339~361)와 `BackToLobby`(771~790)는 **`OnClientConnectedCallback`만 구독 해제하고 `OnClientDisconnectCallback`은 해제하지 않는다.**
→ **의도적으로 나갈 때도 `HandleClientDisconnected`가 호출되어 같은 로그가 찍힌다.**

| 축 | 상태 |
|---|---|
| **축 B** | **`개발` 확정.** `OnServerDisconnected` → UI 통지 경로가 있고(원칙 2), 이 줄에는 **끊긴 원인이 담겨 있지 않다**(축 B ②) |
| **축 A** | **확정 불가.** 정상 종료면 `Info`, 장애면 `Error`인데 **한 줄이 두 상황에 모두 쓰인다.** `Error`로 두면 정상 종료마다 거짓 경보가 쌓이고, `Info`로 두면 진짜 장애가 묻힌다 |

> **이것은 로그 판정 문제가 아니라 코드 문제다.** 어느 축 값을 고르든 틀린다.
> **질문:** ① **의도적 종료 시 `OnClientDisconnectCallback` 구독을 해제**해 두 경우를 갈라 놓고 판정할 것인가(코드 수정 · 4단계 범위 확대),
> ② 아니면 **구분 없이 `Warn` + `개발`로 두고** 별도 작업으로 미룰 것인가?
> **잠정 판정: `Warn` / `개발`** (거짓 경보와 신호 매몰 사이의 중간값이며, 축 B가 `개발`이라 **릴리스에는 어차피 남지 않아 실질 피해가 가장 작다**).

### Q-2. 원칙 1을 적용하면 원인 로그가 범위 밖 파일에 남는다 (2건)

**대상:** `NetworkGameManager.cs:223`(Relay 할당 실패) · `:309`(Relay 참가 실패)

원인(`e.Message`)을 가진 로그는 **`RelayManager.cs:78`·`83`·`122`·`127`** 에 있는데, **`RelayManager.cs`는 이번 8개 파일에 없다.**
→ 원칙 1대로 호출부를 `개발`로 내리면, **원인 로그는 `GameLog`로 이관되지 않아 운영 스트림에 아무것도 남지 않는다.**
(단, `Debug.LogError`는 릴리스 Logcat에는 계속 출력된다 — Research §7-3. **파일·서버 수집 대상이 아닐 뿐이다.**)

> **질문:** ① 호출부를 **`운영`으로 유지**하고 중복을 감수할 것인가,
> ② **`RelayManager.cs`를 이번 범위에 추가**할 것인가(CLAUDE.md 규칙 6 — 범위 확대는 승인 필요),
> ③ **`개발`로 내리고** Relay 계층 이관을 후속 작업으로 둘 것인가?
> **잠정 판정: `Error` / `운영` 유지**(①). **정보를 잃지 않는 쪽을 기본값으로 둔다.**

### Q-3. 원칙 1과 원칙 3이 정면으로 충돌한다 (3건)

**대상:** `NetworkGameManager.cs:201`(초기화 실패) · `:233`(Lobby 생성 실패) · `:288`(Lobby 참가 실패)

세 건 모두 **같은 사건의 원인 로그가 이번 범위 안에 있고 이미 `운영`으로 확정**되었다
(`UnityServicesInitializer.cs:139` / `LobbyManager.cs:111` / `LobbyManager.cs:324`).

| 규칙 | 이 자리에 요구하는 것 |
|---|---|
| **원칙 1** (`LogRules.md` **1.3**) + **금지 사항 9** (**1.14**) | *"같은 사건을 두 곳에서 로깅 금지 — 최종 처리 지점에서 **한 번만**"* → **한쪽은 `개발`이어야 한다** |
| **원칙 3** (`LogRules.md` **1.3**) | *"`Error`는 **항상** `운영`"* (단서는 설정 오류뿐인데 해당하지 않는다) → **양쪽 다 `운영`이어야 한다** |

**이 판정에서 채택한 해석:** 원칙 3은 *"그 **사건**이 운영으로 기록되어야 한다"* 는 뜻이지
*"모든 `Error` **호출문**이 각각 운영이어야 한다"* 는 뜻이 아니다 — 후자로 읽으면 원칙 1이 통째로 무효가 된다.
→ **원인(`e.Message`)을 가진 하위 계층을 `운영`으로 두고, 호출부는 `개발`로 내렸다.**

> **질문: 이 해석이 맞습니까?** 아니라면 `LogRules.md` 1.3에 우선순위를 명시해야 하고,
> **그것은 기준 문서 개정이라 이번 범위 밖이다**(이 작업은 `LogRules.md`를 수정하지 않는다).
> **잠정 판정: `Error` / `개발`** (위 해석대로).

### Q-4. `FirebaseAuthService.cs:463` — 축 판정과 감사(audit) 요구가 어긋난다 (1건)

| 축 | 상태 |
|---|---|
| **축 A** | **`Info` 확정.** 판정 질문은 오직 *"복구되었나?"* 이고, **삭제는 요청대로 성공한 의도된 흐름**이다. *"되돌릴 수 없으니 심각하다"* 는 축 A의 기준이 아니다 |
| **축 B** | **규칙대로면 `개발`.** ①이 "아니오" — 계정 삭제는 개발자가 그대로 재현할 수 있다 |

**그런데 `개발`로 두면 릴리스 빌드에 계정 삭제 기록이 한 줄도 남지 않는다.**
*"내 계정이 사라졌어요"* 라는 문의에 답할 근거가 클라이언트 측에 없어진다.

> **미확인:** Firebase 콘솔에 계정 삭제 감사 기록이 **얼마나 보존되는지 확인하지 못했다.**
> 그것이 충분하다면 축 B `개발`이 그대로 맞고, 부족하다면 감사 요구가 축 판정을 덮어야 한다.
> **추정하지 않는다**(CLAUDE.md 규칙 10).
>
> **질문:** 계정 삭제를 **감사 목적으로 `운영`에 남길 것입니까?**
> 남긴다면 키는 `AccountDeleted` 를 신설하고, UID는 **해시 치환**한다(`LogRules.md` **1.6**).
> **잠정 판정: `Info` / `개발` + UID 해시 치환**(규칙의 문자 그대로).

---

# 5. [3단계] `LogEvent` 키 — 신규 30개

## 5-0. 이름 짓기에 적용한 규칙 (`LogRules.md` **1.5 이벤트 키 — LogEvent**)

| 규정 | 이 목록에서 지킨 방식 |
|------|---------------------|
| **enum 멤버 이름을 그대로(PascalCase) 전송 키로 쓴다** | 변환 규칙을 두지 않았다. 아래 이름이 곧 서버에 쌓이는 키다 |
| **`운영` 로그만 키를 받는다** | 85건에만 부여. `개발` 120건에는 부여하지 않았다 |
| **무엇이 일어났는지를 적는다** (어디서는 `[System/Class]`가 담는다) | 이름에 파일·클래스명을 넣지 않았다 |
| **한 번 정하면 바꾸지 않는다** | 그래서 **파일별로 즉흥 명명하지 않고 85건을 한 번에 놓고 묶었다**(`Plan.md` §5 위험 8) |
| `Unknown`(0번)은 **의도적으로 사용하지 않는다** | 이 목록에 `Unknown` 사용처는 **0건**이다 |

**묶는 기준 두 가지:**
- **같은 사건은 같은 키.** 파일이 달라도 성격이 같으면 하나로 묶었다 — 키가 갈라지면 집계가 쪼개진다.
- **너무 잘게 쪼개지 않는다.** *"업그레이드 골드 부족"* / *"배치 골드 부족"* / *"생산 골드 부족"* 을 세 키로 나누면
  **"자원 부족으로 서버가 거부한 횟수"** 라는 지표 자체가 만들어지지 않는다.
  어느 요청이었는지는 **`[System/Class]` 와 `key=value`** 가 담는다(`LogRules.md` **1.4 형식**).

> **`LogRules.md` 1.5의 이름 예시 3개 중 2개를 그대로 쓰지 않은 이유를 밝힌다.**
> `MatchmakingLobbyJoinFailed` 는 **그대로 채택**했다.
> ~~`ServerRejectedUpgradeInsufficientGold`~~ 는 **채택하지 않았다** — 업그레이드/배치/생산이 세 키로 갈라져
> 위의 "너무 잘게 쪼개지 않는다"에 정면으로 걸리기 때문이다. 대신 `ServerRejectedInsufficientResource` 하나로 묶고
> 요청 종류는 `key=value`로 남긴다. ~~`CloudSaveValueParseFailed`~~ 는 **이번 8개 파일의 사건이 아니다**(`PlayerProfileService.cs`).

---

## 5-1. 키 목록 (30개) — 이름 · 설명 · 해당 로그 위치

### A. 조합 루트 · UseCase 부재 (불변식 위반) — 3키 / 21건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 1 | `ServerRpcGameServicesMissing` | **서버 RPC 처리 중** 조합 루트 또는 UseCase를 얻지 못했다. 발생하면 **이후 모든 같은 종류 요청이 같은 자리에서 죽는다** | `NetworkBuildingController.cs` 142·152·366·376·529·539<br>`NetworkProductionController.cs` 329·340·708·776·825 (**11건**) |
| 2 | `ClientRpcGameServicesMissing` | **클라이언트 RPC 적용 중** 조합 루트 또는 UseCase를 얻지 못했다. 서버 상태가 클라이언트에 반영되지 않는다 | `NetworkBuildingController.cs` 246·253·466·473·619·626<br>`NetworkProductionController.cs` 459·468 (**8건**) |
| 3 | `NetworkControllerSpawnedWithoutGameServices` | 네트워크 컨트롤러가 **스폰 시점에** `IGameServices` 를 찾지 못했다. 1·2번의 **선행 신호**다 | `NetworkBuildingController.cs:58`<br>`NetworkProductionController.cs:72` (**2건**) |

> **왜 1·2를 나누는가:** 근본 원인은 같지만 **결과가 다르다.** 서버 쪽은 게임 전체가 멈추고, 클라이언트 쪽은 화면만 어긋난다.
> 한 키로 묶으면 *"게임이 죽었나 화면만 틀어졌나"* 를 집계에서 구분할 수 없다.
> **왜 3을 따로 두는가:** 1·2와 같은 키로 묶으면 **한 번의 사고가 스폰 1회 + 요청 N회로 부풀려 집계된다**(원칙 1).

### B. 서버가 클라이언트 요청을 거부 — 4키 / 21건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 4 | `ServerRejectedInsufficientResource` | 골드·인구·큐 용량 부족으로 서버가 요청을 거부했다. **클라이언트가 이미 막았어야 하므로 클라·서버 상태 불일치를 뜻한다** | `NetworkBuildingController.cs` 182·412<br>`NetworkProductionController.cs` 380·391·403 (**5건**) |
| 5 | `ServerRejectedUnauthorizedRequest` | 팀 불일치·소유권 불일치·규칙 위반(최고 단계 / Castle 철거) 요청을 서버가 거부했다. **정상 클라이언트는 보낼 수 없는 요청이라 변조 탐지 신호로 읽는다** | `NetworkBuildingController.cs` 171·394·403·556·565<br>`NetworkProductionController.cs` 357·699·764·817 (**9건**) |
| 6 | `ServerRejectedTargetNotFound` | 요청 대상(건물·배럭 생산 상태)이 서버에 없다. 클라이언트는 **존재하는 대상의 패널만** 열 수 있으므로 상태 불일치다 | `NetworkBuildingController.cs` 385·548<br>`NetworkProductionController.cs:369` (**3건**) |
| 7 | `ServerActionExecutionFailed` | **검증을 모두 통과한 뒤 실행이 실패했다.** 서버 내부 상태 이상이며 6번(대상 없음)과 원인이 다르다 | `NetworkBuildingController.cs` 200·424<br>`NetworkProductionController.cs` 718·834 (**4건**) |

> **함께 남길 `key=value` (권고):** `Request=Build|Upgrade|Demolish|Enqueue|CancelSlot|SetRally|ToggleAuto`,
> `Reason=Gold|Population|QueueFull|Team|Ownership|MaxStage|CastleProtected`, `Team=`, `ClientId=`.
> **집계 단위는 키가, 세부 분해는 필드가 담당한다**(`LogRules.md` **1.4 형식**).

### C. 클라이언트 상태 동기화 실패 — 2키 / 4건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 8 | `ClientStateSyncApplyFailed` | 서버가 보낸 스폰·업그레이드를 **클라이언트가 적용하지 못했다. 재시도 경로가 없어 그 오브젝트는 영구히 누락된다** | `NetworkBuildingController.cs` 272·490<br>`NetworkProductionController.cs:484` (**3건**) |
| 9 | `UnitViewInitializeTimeout` | 유닛 뷰 초기화 재시도가 제한 시간을 넘겨 실패했다. **8번과 달리 재시도를 거친 뒤의 최종 실패다** | `NetworkProductionController.cs:539` (**1건**) |

### D. 로비 서비스 — 4키 / 13건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 10 | `MatchmakingLobbyJoinFailed` | **랜덤 매칭 경로**의 로비 생성/참가/검색이 실패했다. 호출부는 `null`만 받아 *"다시 시도해주세요"* 만 띄운다 | `LobbyManager.cs` 184·280 (**2건**) |
| 11 | `LobbyServiceCallFailed` | 그 외 로비 서비스 호출이 예외로 실패했다(생성·조회·참가·코드 갱신·퇴장) | `LobbyManager.cs` 111·222·304·324·343·381·409·446 (**8건**) |
| 12 | `LobbyHeartbeatFailed` | Host Heartbeat 전송이 실패했다. **계속되면 Lobby가 만료돼 매칭이 조용히 끊긴다** | `LobbyManager.cs:498` (**1건**) |
| 13 | `LobbyInvariantViolated` | 로비 조작에 필요한 전제가 깨져 있다(`matchId` 비어 있음 / `CurrentLobby` 없음) | `LobbyManager.cs` 153·361 (**2건**) |

> **왜 10과 11을 나누는가:** `LogRules.md` **1.2** 조합표가 *"매칭 로비 생성/참가 실패"* 를 별도 사례로 들었고,
> 랜덤 매칭 실패율은 **그 자체가 독립적으로 봐야 하는 지표**다. 일반 로비 실패와 섞이면 매칭 품질을 볼 수 없다.
> **함께 남길 `key=value`:** `Operation=CreateLobby|Query|JoinById|JoinByCode|QuickJoin|UpdateRelayCode|Refresh|Leave`.

### E. Relay · 네트워크 세션 — 4키 / 8건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 14 | `RelaySetupFailed` | Relay 할당·참가·Join Code 확보 중 하나가 실패해 세션을 열 수 없다 | `NetworkGameManager.cs` 223·299·309 (**3건**) — **223·309는 질의 Q-2 대상** |
| 15 | `NetworkManagerSingletonMissing` | `NetworkManager.Singleton` 이 null이다. **`GameBootstrapper`가 유일한 조합 루트라는 전제가 깨진 상태다** | `NetworkGameManager.cs` 713·734 (**2건**) |
| 16 | `NetworkSessionStartFailed` | `StartHost()` / `StartClient()` 가 `false`를 반환했다. 15번과 달리 **객체는 있는데 시작이 거부된 것**이다 | `NetworkGameManager.cs` 721·742 (**2건**) |
| 17 | `SceneLoadRequestedByNonServer` | 서버가 아닌 쪽에서 게임 씬 로드를 요청했다. **정상 흐름에서는 발생할 수 없다** | `NetworkGameManager.cs:667` (**1건**) |

> **함께 남길 `key=value`:** 14번은 `Stage=Allocate|Join|CodeMissing`, 15·16번은 `Role=Host|Client`.

### F. 매칭 · 세션 시작 예외 — 3키 / 6건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 18 | `GameSessionStartUnhandledException` | 게임 생성/참가 흐름의 **최종 `catch`** 에서 잡힌 예외. 하위 계층이 분류하지 못한 것이 여기로 온다 | `NetworkGameManager.cs` 260·329·484·543 (**4건**) |
| 19 | `MatchmakingUnhandledException` | 랜덤 매칭 흐름에서 예외가 났다. **`catch` 본문이 로그 한 줄뿐이고 `OnError`도 없어 매칭이 조용히 죽는다** | `NetworkGameManager.cs:402` (**1건**) |
| 20 | `MatchmakingTicketDeleteFailed` | 매칭 취소 시 티켓 삭제가 실패했다. 흐름은 계속되지만 **서버에 티켓이 남아 이후 매칭을 방해할 수 있다** | `NetworkGameManager.cs:631` (**1건**) |

> **함께 남길 값:** 18·19번은 `GameLog`의 **`Exception` 오버로드로 예외 객체를 그대로 넘긴다** —
> `TimeoutException` 300건과 `NullReferenceException` 300건은 완전히 다른 사건이고,
> 메시지 문자열로 눌러 담으면 그 구분이 사라진다(`LogRules.md` **1.9 예외 처리**).
> **`Flow=Host|Join|MatchHost|MatchJoin`** 을 함께 남겨 18번 안에서 경로를 분해한다.

### G. UGS 초기화 · 브릿지 — 6키 / 7건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 21 | `UnityServicesInitializeFailed` | UGS 초기화 자체가 실패했다. **지역·회선·프로젝트 설정에 좌우돼 개발 기기에서 재현되지 않는다** | `UnityServicesInitializer.cs:139` (**1건**) |
| 22 | `UgsSessionMissingAnonymousFallback` | UGS 세션이 없어 **익명 로그인으로 폴백했다. 릴리스에서 이것이 뜨면 OIDC 세션이 유실돼 PlayerId가 통째로 바뀐 것이고, 플레이어는 통지받지 못한다** | `UnityServicesInitializer.cs:129` (**1건**) |
| 23 | `UgsBridgeFailed` | Firebase ↔ UGS 브릿지가 실패했다. **로그인은 성공으로 처리되므로 플레이어는 멀티플레이가 막힌 이유를 모른다** | `LoginUseCase.cs:494` (**1건**) |
| 24 | `UgsBridgeUnhandledException` | 브릿지 **바깥**에서 잡힌 예외. `BridgeToUGSAsync`가 내부에서 모든 예외를 삼키므로 **여기 오는 것은 23번과 다른 예외다** | `LoginUseCase.cs` 143·315 (**2건**) |
| 25 | `UgsBridgeMissingFirebaseUid` | 브릿지 호출에 Firebase UID가 비어 있었다. 호출부 계약 위반이다 | `LoginUseCase.cs:446` (**1건**) |
| 26 | `UgsSignOutFailed` | UGS 세션 정리가 실패했다. **다음 로그인에서 이전 계정 세션이 남아 계정이 뒤바뀔 수 있다** | `LoginUseCase.cs:389` (**1건**) |

### H. Firebase 인증 — 4키 / 5건

| # | 키 | 설명 | 해당 로그 위치 |
|:-:|---|------|---|
| 27 | `FirebaseDependencyUnavailable` | Firebase 의존성(Google Play 서비스 등)을 해결하지 못했다. **기기마다 다르다** | `FirebaseAuthService.cs:107` (**1건**) |
| 28 | `FirebaseInitializeFailed` | Firebase 초기화가 예외로 실패했다. **호출부는 `false`만 받아 사유를 모른다** | `FirebaseAuthService.cs:121` (**1건**) |
| 29 | `GooglePlayGamesAuthFailed` | GPGS 인증 또는 Server Auth Code 발급이 실패했다. **실패 코드가 이 로그에만 있고 UI 문구에는 담기지 않는다** | `FirebaseAuthService.cs` 220·233 (**2건**) |
| 30 | `FirebaseAuthOperationFailed` | **모든 `FirebaseException`이 지나는 단일 변환 지점.** 원본 `code`·`reason`이 여기에만 남는다 | `FirebaseAuthService.cs:550` (**1건**) |

> **함께 남길 `key=value`:** 29번은 `Stage=Authenticate|ServerAuthCode`,
> 30번은 `Operation=` (익명 로그인 / Google 로그인 / 이메일 로그인 / 회원가입 / 인증 메일 발송 / 계정 삭제 …) 와 `Code=` · `Reason=`.
> **30번은 `operation` 인자를 이미 받고 있어 그대로 필드로 옮기면 된다.**

---

## 5-2. 키 커버리지 검증

| 구분 | 건수 |
|------|:-:|
| A 조합 루트·UseCase 부재 | 21 |
| B 서버 거부 | 21 |
| C 동기화 실패 | 4 |
| D 로비 서비스 | 13 |
| E Relay·세션 | 8 |
| F 매칭·세션 예외 | 6 |
| G UGS | 7 |
| H Firebase | 5 |
| **합계** | **85** |

**`운영` 판정 85건 = 키 부여 85건.** 누락 0건 (`LogRules.md` **1.14 금지 사항** 6 충족).
**`개발` 120건에는 키를 부여하지 않았다** (**1.5** — `개발`·`임시`는 전송 대상이 아니다).
**`Unknown` 사용처 0건.**

---

## 5-3. `LogEvent` enum 확장 초안 — **문서 제시용. 코드는 수정하지 않았다**

> ⚠️ **이 블록은 4단계에서 반영한다. 이번 단계에서 `Application/Interfaces/ILogSink.cs` 를 열어 고치지 않았다.**
> 아래는 **기존 두 멤버(`Unknown` · `UnhandledException`) 뒤에 추가할 30개**이며,
> **기존 멤버의 이름·순서·값은 건드리지 않는다**(이름이 바뀌면 과거 지표와 연결이 끊긴다 — `LogRules.md` **1.5**).

```csharp
    // ── 이하 30개: 네트워크·인증 계층 209(재실측 205)건 이관에서 추가 ──────────
    //    부여 근거: _Tasks/2026-08-13/07_13_network-auth-log-cleanup/LogAudit.md §5-1

    // [A] 조합 루트 · UseCase 부재 (불변식 위반)
    ServerRpcGameServicesMissing,
    ClientRpcGameServicesMissing,
    NetworkControllerSpawnedWithoutGameServices,

    // [B] 서버가 클라이언트 요청을 거부
    ServerRejectedInsufficientResource,
    ServerRejectedUnauthorizedRequest,
    ServerRejectedTargetNotFound,
    ServerActionExecutionFailed,

    // [C] 클라이언트 상태 동기화 실패
    ClientStateSyncApplyFailed,
    UnitViewInitializeTimeout,

    // [D] 로비 서비스
    MatchmakingLobbyJoinFailed,
    LobbyServiceCallFailed,
    LobbyHeartbeatFailed,
    LobbyInvariantViolated,

    // [E] Relay · 네트워크 세션
    RelaySetupFailed,
    NetworkManagerSingletonMissing,
    NetworkSessionStartFailed,
    SceneLoadRequestedByNonServer,

    // [F] 매칭 · 세션 시작 예외
    GameSessionStartUnhandledException,
    MatchmakingUnhandledException,
    MatchmakingTicketDeleteFailed,

    // [G] UGS 초기화 · 브릿지
    UnityServicesInitializeFailed,
    UgsSessionMissingAnonymousFallback,
    UgsBridgeFailed,
    UgsBridgeUnhandledException,
    UgsBridgeMissingFirebaseUid,
    UgsSignOutFailed,

    // [H] Firebase 인증
    FirebaseDependencyUnavailable,
    FirebaseInitializeFailed,
    GooglePlayGamesAuthFailed,
    FirebaseAuthOperationFailed
```

> **질의 Q-4에서 "계정 삭제를 운영에 남긴다"는 답이 오면 `AccountDeleted` 를 추가한다(31번째).**
> 질의 Q-1~Q-3의 답은 **축 판정만 바꾸며 키 목록에는 영향을 주지 않는다**
> (Q-2·Q-3의 대상 5건은 이미 `RelaySetupFailed` / `개발`로 잠정 배치되어 있다).

---

# 6. 5단계 마스킹 대상 — 참고 목록 → **[2026-08-17] 처리 완료**

`LogRules.md` **1.6 민감 데이터** 대상. **`개발` 로그에도 그대로 적용된다** (*"에디터 포함 항상 적용"*).

> ## ✅ [2026-08-17 갱신] 5단계 처리 완료 — 커밋 `4e027e68` · `675203ae`
>
> **아래 표는 1~3단계 감사 시점(2026-08-13)의 참고 목록이며, 원문 그대로 보존한다.**
> 실제 처리 결과와 **두 군데가 어긋났다.** 감사표의 신뢰도에 관한 정보이므로 지우지 않고 여기 남긴다.
>
> ### ⚠️ 차이 1 — 이메일은 **3건이 아니라 실제 출력 자리 1곳**이었다
>
> 아래 표는 이메일을 **3건**(`FirebaseAuthService.cs` `318` · `365` · `424`)으로 세었다.
> 그러나 **실제 코드에서 이메일을 출력하던 자리는 1곳뿐**이었다 — `"이메일 연동 성공"`.
>
> | 자리 | 실제 상태 | 처리 |
> |------|----------|------|
> | `"이메일 연동 성공"` | **이메일을 출력하고 있었다** | **출력 제거** |
> | `"인증 메일 발송"` | **4단계 이관 과정에서 이미 출력이 빠져 있었다** | **표식 주석만 제거** |
> | `"비밀번호 재설정 메일 발송"` | **4단계 이관 과정에서 이미 출력이 빠져 있었다** | **표식 주석만 제거** |
>
> 라인 번호 기준 목록은 4단계 이관으로 이미 무효가 되었고(`Plan.md` §0-7 ⑤),
> **표식 주석이 붙은 자리를 센 확정값은 15곳**이다.
>
> ### ⚠️ 차이 2 — **이 목록에 없던 마스킹 대상이 1곳 있었다** (감사 누락)
>
> `Infrastructure/Network/LobbyManager.cs` 의 `"CreateOrJoin 완료"` 로그가 **`HostId` 를 평문 출력**하고 있었다.
> **이 자리는 아래 표 어디에도 없다.** 즉 **이 감사표는 마스킹 대상을 전수 포착하지 못했다.**
>
> - **`HostId` 가 UGS PlayerId 라는 근거:** 같은 파일 **60행**이
>   `CurrentLobby.HostId == AuthenticationService.Instance.PlayerId` 로 **직접 비교**한다.
>   따라서 아래 표의 「UGS PlayerId — `GameLog.HashId` 로 해시 치환」 행에 들어갔어야 할 자리다.
> - **조치 (커밋 `675203ae`):** `GameLog.HashId(...)` 경유로 수정.
>   **로직 판정에 쓰는 60행은 원본 유지** — 비교 대상을 해시로 바꾸면 판정이 깨진다.
> - **왜 놓쳤는가:** 이 감사는 필드 **이름**(`Uid=` · `PlayerId=` · `Email=`)을 축으로 훑었는데,
>   `HostId` 는 그 이름 중 어느 것도 아니면서 **값은 PlayerId** 인 자리였다.
>   **필드 이름만으로 훑는 방식의 한계**가 그대로 드러난 사례로 기록해 둔다.
>
> ### 처리 결과 요약 (실측 — 2026-08-17)
>
> | 파일 | 곳 | 처리 |
> |------|:-:|------|
> | `Infrastructure/Auth/FirebaseAuthService.cs` | **10** | UID 8곳 해시 / 이메일 1곳 출력 제거 / 이미 처리돼 있던 2곳은 표식 주석만 제거 |
> | `Infrastructure/Network/UnityServicesInitializer.cs` | **2** | PlayerId 해시 |
> | `Application/UseCases/LoginUseCase.cs` | **2** | UID · PlayerId 해시 |
> | `Infrastructure/Network/NetworkGameManager.cs` | **1** | PlayerId 해시 |
> | **소계 (이 목록 기준)** | **15** | |
> | `Infrastructure/Network/LobbyManager.cs` | **+1** | **이 감사표에 없던 자리** — `HostId` 해시 |
>
> **잔존 확인:** `⚠️ 5단계(마스킹) 대상` 표식 주석 **0건** · `GameLog.HashId` 실제 호출 **14건**
> (`grep` 15건 중 1건은 근거 주석) · 위 4파일에서 `Uid=`·`PlayerId=`·`Email=` 중
> 해시를 거치지 않은 자리 **0건**이고 `Email=` 은 **완전히 사라졌다**.
>
> **같은 파일의 `MatchId=` · `Name=` 은 판단하지 않고 그대로 두었다** — §7 ② 가
> *"규정 항목 아님 — 미확인으로 남긴다"* 로 분류한 **준식별자**이기 때문이다(CLAUDE.md 규칙 10·12).
>
> 전체 진행 기록은 `Plan.md` **§0-9** 참조.

| 처리 | 대상 | 위치 |
|------|------|------|
| **출력 자체를 없앤다** (부분 마스킹도 금지) | **이메일** | `FirebaseAuthService.cs` 318 · 365 · 424 (**3건**) |
| **`GameLog.HashId` 로 해시 치환** | **Firebase UID** | `FirebaseAuthService.cs` 115 · 146 · 191 · 265 · 289 · 399 · 424 · 463<br>`LoginUseCase.cs:131` (**9건**) |
| **`GameLog.HashId` 로 해시 치환** | **UGS PlayerId** | `UnityServicesInitializer.cs` 123 · 134<br>`NetworkGameManager.cs:196`<br>`LoginUseCase.cs:485` (**4건**) |
| **확인 완료 — 조치 불필요** | 인증 토큰 | `FirebaseAuthService.cs:503` 은 **토큰 값을 출력하지 않는다.** `LoginUseCase.cs:478` 도 마찬가지다 |

**합계 16건** (`:424`는 이메일·UID **2종을 동시에** 출력하므로 두 줄에 모두 등장한다 — 실제 로그 라인 수는 **15줄**).

> **Research §9의 19건과 다른 이유:** Research는 `LobbyManager.cs` 104·177·299·319·338 의
> **Lobby 이름 / matchId 를 "준식별자"로 함께 셌다**(5건). `LogRules.md` **1.6**의 규정 항목은
> **이메일 · UID · PlayerId · 인증 토큰 · 세션 키 · 비밀번호**이고 **Lobby 이름·matchId는 명시돼 있지 않다.**
> → **미확인으로 남긴다**(§7 ②). 15 + 5 = 20 이 아니라 19인 것은 Research가 `LoginUseCase.cs:485`(PlayerId)를
> UID 항목과 분리해 세는 등 분류 축이 달랐기 때문이다. **이번 목록은 규정 항목에 정확히 걸리는 것만 담았다.**

---

# 7. 미확인 항목 (추정하지 않고 남긴다 — CLAUDE.md 규칙 10)

| # | 항목 | 왜 확인하지 못했는가 |
|:-:|------|---------------------|
| ① | **Firebase 콘솔의 계정 삭제 감사 기록 보존 기간** | 외부 서비스 설정이라 코드로 확인할 수 없다. **질의 Q-4의 판단 근거**다 |
| ② | **Lobby Code · Lobby 이름 · matchId 가 `LogRules.md` 1.6의 "세션 키"에 해당하는가** | **규정에 명시돼 있지 않다.** Lobby Code는 그 방에 참가할 수 있는 값이라 세션 키로 볼 여지가 있으나, 해당 로그 6건은 **전부 `개발` 판정이라 릴리스에는 남지 않는다** — 실질 노출 경로는 에디터 로그 파일 공유뿐이다 |
| ③ | **`DisplayName`(`FirebaseAuthService.cs:191`) 의 취급** | `LogRules.md` **1.6** 에 항목이 없다. 개인 식별 가능성이 있으나 **규정 공백이라 판단을 보류한다** |
| ④ | **로그 인자 안에 부수효과가 있는 식이 있는가** | Research §10-④ 그대로 **미해소.** 205건을 읽는 동안 발견하지 못했으나 **전수 정적 검증은 수행하지 않았다.** 4단계 이관 시 개별 확인이 필요하다 |
| ⑤ | **`NetworkGameEndController.cs` 에 실패 경로 로그가 없는 것이 의도인가** | Research §10-③ 그대로 **미해소.** 다만 **이 파일의 운영 로그가 0건이라는 사실은 이번 재판정으로 확정**되었다(§3-1) |
| ⑥ | **컴파일 검증** | 이 환경에 Unity가 없다. **1~3단계는 코드를 수정하지 않으므로 컴파일 대상이 없다.** `Plan.md` §0-3의 **0단계(뼈대 컴파일 검증)는 여전히 4단계의 선행 조건**이다 |

---

# 8. 이번 단계에서 하지 않은 것 (명시)

- **코드·프리팹·씬·에셋을 수정하지 않았다.** 코드는 읽기만 했다.
- **`Application/Interfaces/ILogSink.cs` 를 수정하지 않았다.** §5-3은 **문서 제시용 초안**이며 반영은 4단계다.
- **`LogRules.md` 를 수정하지 않았다.** 판정 기준의 단일 소스이므로 이 작업이 손대지 않는다.
- **다른 task 문서(`05_05_…`)를 수정하지 않았다.**
- **`Testcase.md` 를 작성하지 않았다** (`WORKFLOW.md` [5-1] — 사용자 명시 요청 시에만).
- **상시 참조 문서를 갱신하지 않았다** — 구현이 끝나지 않았다(`WORKFLOW.md` [11] ②).
- **git 명령을 실행하지 않았다** (CLAUDE.md 규칙 5).
- **`.claude/agent-memory/` 를 수정하지 않았다.**
